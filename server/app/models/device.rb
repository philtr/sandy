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

  def self.digest_token(token)
    Digest::SHA256.hexdigest(token.to_s)
  end

  def self.authenticate_token(token)
    return if token.blank?

    not_revoked.find_by(token_digest: digest_token(token))
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
    online?(at:) ? timer_status(at:) : "offline"
  end

  def remaining_seconds(at: Time.current)
    return 0 unless expires_at

    [ (expires_at - at).ceil, 0 ].max
  end

  def timer_snapshot(at: Time.current)
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
end
