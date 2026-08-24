class Device < ApplicationRecord
  ONLINE_WINDOW = 75.seconds
  HEARTBEAT_INTERVAL_SECONDS = 30

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

    device = find_by(token_digest: digest_token(token))
    return if device&.revoked_at? && !device.family.allow_revoked_devices?

    device
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
      remaining_seconds: remaining_seconds(at:),
      timer_status: timer_status(at:),
      heartbeat_interval_seconds: HEARTBEAT_INTERVAL_SECONDS
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
    update!(revoked_at: Time.current, state_version: state_version + 1)
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
        update!(expires_at: now, state_version: state_version + 1)
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
    errors.add(:archived_at, "requires the PC to be revoked first") if archived_at? && !revoked_at?
  end
end
