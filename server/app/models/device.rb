class Device < ApplicationRecord
  class InactiveTimerError < StandardError; end

  ONLINE_WINDOW = 75.seconds
  HEARTBEAT_INTERVAL_SECONDS = 30
  LAUNCHER_EDIT_DURATION = 30.minutes

  belongs_to :family
  has_many :time_grants, dependent: :destroy
  has_many :device_events, dependent: :destroy

  validates :name, presence: true, length: { maximum: 100 }
  validates :platform, presence: true
  validates :state_version, numericality: { only_integer: true, greater_than_or_equal_to: 0 }

  scope :not_revoked, -> { where(revoked_at: nil) }
  scope :not_archived, -> { where(archived_at: nil) }
  scope :revoked, -> { where.not(revoked_at: nil) }

  validate :archived_only_after_revocation

  def self.digest_token(token)
    Digest::SHA256.hexdigest(token.to_s)
  end

  def self.authenticate_token(token)
    return if token.blank?

    not_revoked.find_by(token_digest: digest_token(token))
  end

  def self.revoked_token?(token)
    revoked_token_device(token).present?
  end

  def self.revoked_token_device(token)
    return if token.blank?

    find_by(revoked_token_digest: digest_token(token))
  end

  def issue_token!
    token = SecureRandom.urlsafe_base64(32)
    update!(token_digest: self.class.digest_token(token))
    token
  end

  def online?(at: Time.current)
    last_heartbeat_at.present? && last_heartbeat_at >= at - ONLINE_WINDOW
  end

  def timer_status(at: Time.current)
    expires_at.present? && expires_at > at ? "active" : "expired"
  end

  def dashboard_status(at: Time.current)
    return "revoked" if revoked_at?

    online?(at:) ? timer_status(at:) : "offline"
  end

  def remaining_seconds(at: Time.current)
    return 0 unless expires_at

    [ (expires_at - at).ceil, 0 ].max
  end

  def timer_snapshot(at: Time.current)
    return family.revoked_device_release_snapshot(at:, state_version:) if revoked_at? && family.allow_revoked_devices?

    {
      schema_version: 1,
      state_version: state_version,
      server_time: at.iso8601(3),
      expires_at: expires_at&.iso8601(3),
      allowance_started_at: allowance_started_at&.iso8601(3),
      launcher_edit_unlocked_until: launcher_edit_unlocked_until&.iso8601(3),
      remaining_seconds: remaining_seconds(at:),
      timer_status: timer_status(at:),
      heartbeat_interval_seconds: HEARTBEAT_INTERVAL_SECONDS,
      voice_theme: family.voice_theme
    }
  end

  def broadcast_timer_state!
    state = timer_snapshot
    DeviceChannel.broadcast_to(self, type: "timer_state", timer_state: state)
    FamilyChannel.broadcast_to(family, type: "device_state", device_id: id, timer_state: state.merge(connectivity: dashboard_status))
  end

  def record_heartbeat!(agent_version:, overlay_active:, metadata: {})
    update!(
      last_heartbeat_at: Time.current,
      agent_version: agent_version.presence || self.agent_version,
      overlay_active: ActiveModel::Type::Boolean.new.cast(overlay_active),
      metadata: self.metadata.merge(metadata.to_h.slice("os_version", "machine_name"))
    )
    FamilyChannel.broadcast_to(family, type: "heartbeat", device_id: id, connectivity: dashboard_status)
  end

  def revoke!
    transaction do
      lock!
      update!(
        revoked_at: Time.current,
        revoked_token_digest: token_digest,
        token_digest: nil,
        launcher_edit_unlocked_until: nil,
        state_version: state_version + 1
      )
    end

    # Legacy agents only understand timer snapshots, while current agents can
    # immediately discard their credential and return to enrollment.
    broadcast_timer_state! if family.allow_revoked_devices?
    DeviceChannel.broadcast_to(self, type: "device_revoked")
  end

  def launcher_edit_unlocked?(at: Time.current)
    launcher_edit_unlocked_until.present? && launcher_edit_unlocked_until > at
  end

  def unlock_launcher_edit!(parent_profile:, now: Time.current)
    transaction do
      lock!
      raise InactiveTimerError unless timer_status(at: now) == "active" && !revoked_at?

      unlocked_until = now + LAUNCHER_EDIT_DURATION
      update!(launcher_edit_unlocked_until: unlocked_until, state_version: state_version + 1)
      device_events.create!(
        event_id: SecureRandom.uuid,
        kind: "launcher_edit_unlocked",
        occurred_at: now,
        details: {
          parent_profile_id: parent_profile.id,
          parent_profile: parent_profile.name,
          unlocked_until: unlocked_until.iso8601(3)
        }
      )
      unlocked_until
    end.tap { broadcast_timer_state! }
  end

  def lock_launcher_edit!(parent_profile:, now: Time.current)
    transaction do
      lock!
      update!(launcher_edit_unlocked_until: nil, state_version: state_version + 1)
      device_events.create!(
        event_id: SecureRandom.uuid,
        kind: "launcher_edit_locked",
        occurred_at: now,
        details: {
          parent_profile_id: parent_profile.id,
          parent_profile: parent_profile.name
        }
      )
    end
    broadcast_timer_state!
  end

  def archive!
    update!(archived_at: Time.current)
  end

  def revoke_screen_time!(parent_profile:, idempotency_key:, now: Time.current)
    event_id = "screen-time-revocation:#{Digest::SHA256.hexdigest(idempotency_key.to_s)}"
    existing = device_events.find_by(event_id:)
    return existing if existing

    event = transaction do
      lock!
      existing = device_events.find_by(event_id:)
      if existing
        existing
      else
        previous = expires_at
        event = device_events.create!(
          event_id:,
          kind: "screen_time_revoked",
          occurred_at: now,
          details: {
            parent_profile_id: parent_profile.id,
            parent_profile: parent_profile.name,
            previous_expires_at: previous&.iso8601(3),
            resulting_expires_at: now.iso8601(3)
          }
        )
        update!(expires_at: now, allowance_started_at: nil, state_version: state_version + 1)
        event
      end
    end

    broadcast_timer_state!
    event
  rescue ActiveRecord::RecordNotUnique
    device_events.find_by!(event_id:)
  end

  private

  def archived_only_after_revocation
    errors.add(:archived_at, "requires the PC to be unenrolled first") if archived_at? && !revoked_at?
  end
end
