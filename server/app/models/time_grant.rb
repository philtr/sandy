class TimeGrant < ApplicationRecord
  class EventConflict < StandardError; end

  EVENT_KIND = "time_granted"
  EVENT_ID_PREFIX = "time-grant"

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
      allowance_started_at = previous.present? && previous > now ? (device.allowance_started_at || now) : now
      grant = create!(
        device: device,
        parent_profile: parent_profile,
        duration_seconds: duration_seconds,
        previous_expires_at: previous,
        resulting_expires_at: resulting,
        idempotency_key: idempotency_key
      )
      grant.create_device_event!
      device.update!(
        expires_at: resulting,
        allowance_started_at: allowance_started_at,
        state_version: device.state_version + 1
      )
      grant
    end
  rescue ActiveRecord::RecordNotUnique
    existing = find_by(device:, idempotency_key:)
    return existing if existing

    raise
  end

  def device_event_id
    "#{EVENT_ID_PREFIX}:#{id}"
  end

  def create_device_event!
    device.device_events.create!(
      event_id: device_event_id,
      kind: EVENT_KIND,
      occurred_at: created_at,
      details: {
        time_grant_id: id,
        parent_profile_id: parent_profile_id,
        parent_profile: parent_profile.name,
        duration_seconds: duration_seconds,
        previous_expires_at: previous_expires_at&.iso8601(3),
        resulting_expires_at: resulting_expires_at.iso8601(3)
      }
    )
  end

  def ensure_device_event!
    event = device.device_events.find_by(event_id: device_event_id)
    return event if event_matches_grant?(event)
    raise EventConflict, event_conflict_message(event) if event

    create_device_event!
  rescue ActiveRecord::RecordNotUnique
    event = device.device_events.find_by!(event_id: device_event_id)
    return event if event_matches_grant?(event)

    raise EventConflict, event_conflict_message(event)
  end

  private

  def event_matches_grant?(event)
    event&.kind == EVENT_KIND && event.details["time_grant_id"].to_i == id
  end

  def event_conflict_message(event)
    "Device event #{device_event_id.inspect} already exists and does not represent time grant #{id}"
  end
end
