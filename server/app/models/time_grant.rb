class TimeGrant < ApplicationRecord
  QUICK_GRANT_MINUTES = [ 1, 5, 15, 30, 60 ].freeze
  ALLOWED_DURATIONS = QUICK_GRANT_MINUTES.map { |minutes| minutes.minutes.to_i }.freeze

  belongs_to :device
  belongs_to :parent_profile

  validates :duration_seconds, inclusion: { in: ALLOWED_DURATIONS }
  validates :idempotency_key, presence: true, length: { maximum: 100 }, uniqueness: { scope: :device_id }
  validates :resulting_expires_at, presence: true

  after_create_commit -> { device.broadcast_timer_state! }

  def self.grant!(device:, parent_profile:, duration_seconds:, idempotency_key:, now: Time.current)
    existing = find_by(device:, idempotency_key:)
    return existing if existing

    transaction do
      device.lock!
      existing = find_by(device:, idempotency_key:)
      return existing if existing

      previous = device.expires_at
      resulting = [ previous, now ].compact.max + duration_seconds.to_i.seconds
      grant = create!(
        device: device,
        parent_profile: parent_profile,
        duration_seconds: duration_seconds,
        previous_expires_at: previous,
        resulting_expires_at: resulting,
        idempotency_key: idempotency_key
      )
      device.update!(expires_at: resulting, state_version: device.state_version + 1)
      grant
    end
  rescue ActiveRecord::RecordNotUnique
    find_by!(device:, idempotency_key:)
  end
end
