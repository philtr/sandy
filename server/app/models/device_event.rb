class DeviceEvent < ApplicationRecord
  AGENT_DIAGNOSTIC_KIND = "agent_diagnostic"
  AGENT_DIAGNOSTIC_RETENTION = 250
  MAX_DETAILS_BYTES = 4.kilobytes
  AGENT_EVENT_KINDS = %w[
    agent_started
    startup
    reconnect
    warning_shown
    final_countdown_shown
    overlay_shown
    overlay_hidden
    update_downloaded
    update_failed
    update_applying
    agent_diagnostic
  ].freeze
  DIAGNOSTIC_SEVERITIES = %w[info warning error].freeze

  belongs_to :device

  scope :agent_diagnostics, -> { where(kind: AGENT_DIAGNOSTIC_KIND) }

  validates :event_id, :kind, :occurred_at, presence: true
  validates :event_id, uniqueness: { scope: :device_id }, length: { maximum: 100 }
  validates :kind, length: { maximum: 60 }
  validate :details_are_bounded
  validate :diagnostic_details_are_structured, if: :agent_diagnostic?

  def agent_diagnostic?
    kind == AGENT_DIAGNOSTIC_KIND
  end

  def diagnostic_line
    diagnostic = details.stringify_keys
    context = diagnostic["context"].presence
    exception = diagnostic["exception"].presence
    suffix = [ context, exception ].compact.map(&:to_json)
    line = "#{occurred_at.utc.iso8601(3)} #{diagnostic['severity'].to_s.upcase} #{diagnostic['component']} #{diagnostic['code']} — #{diagnostic['message']}"
    suffix.empty? ? line : "#{line} #{suffix.join(' ')}"
  end

  private

  def details_are_bounded
    return if details.to_json.bytesize <= MAX_DETAILS_BYTES

    errors.add(:details, "must be #{MAX_DETAILS_BYTES} bytes or less")
  end

  def diagnostic_details_are_structured
    diagnostic = details.stringify_keys
    errors.add(:details, "has an invalid severity") unless DIAGNOSTIC_SEVERITIES.include?(diagnostic["severity"])
    validate_diagnostic_text(diagnostic, "component", 60)
    validate_diagnostic_text(diagnostic, "code", 80)
    validate_diagnostic_text(diagnostic, "message", 500)
    errors.add(:details, "context must be an object") if diagnostic.key?("context") && !diagnostic["context"].is_a?(Hash)
    errors.add(:details, "exception must be an object") if diagnostic.key?("exception") && !diagnostic["exception"].is_a?(Hash)
  end

  def validate_diagnostic_text(diagnostic, key, maximum)
    value = diagnostic[key]
    errors.add(:details, "#{key} is required") if value.blank?
    errors.add(:details, "#{key} is too long") if value.to_s.length > maximum
  end
end
