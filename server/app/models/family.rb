class Family < ApplicationRecord
  REVOKED_RELEASE_DURATION = 100.years
  VOICE_THEMES = %w[stella blondie random].freeze

  has_one :account, dependent: :destroy
  has_many :parent_profiles, dependent: :destroy
  has_many :devices, dependent: :destroy

  has_secure_password :enrollment_code, validations: false

  validates :name, :timezone, :enrollment_code_digest, presence: true
  validates :voice_theme, inclusion: { in: VOICE_THEMES }
  validate :timezone_must_exist

  def self.generate_join_code
    alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"
    Array.new(3) { Array.new(4) { alphabet[SecureRandom.random_number(alphabet.length)] }.join }.join("-")
  end

  def authenticate_join_code(code)
    authenticate_enrollment_code(self.class.normalize_join_code(code))
  end

  def revoked_device_release_snapshot(at: Time.current, state_version: nil)
    # Agent 1.1.0 only understands active and expired. Return a normal schema-1
    # active snapshot instead of adding another timer status.
    expires_at = at + REVOKED_RELEASE_DURATION
    {
      schema_version: 1,
      state_version: state_version || devices.maximum(:state_version).to_i + 1,
      server_time: at.iso8601(3),
      expires_at: expires_at.iso8601(3),
      remaining_seconds: (expires_at - at).ceil,
      timer_status: "active",
      heartbeat_interval_seconds: Device::HEARTBEAT_INTERVAL_SECONDS,
      voice_theme: voice_theme
    }
  end

  def update_voice_theme!(theme)
    transaction do
      lock!
      update!(voice_theme: theme)
      devices.not_revoked.update_all([ "state_version = state_version + 1, updated_at = ?", Time.current ])
    end

    devices.not_revoked.find_each(&:broadcast_timer_state!)
  end

  def self.normalize_join_code(code)
    code.to_s.upcase.gsub(/[^A-Z0-9]/, "")
  end

  private

  def timezone_must_exist
    ActiveSupport::TimeZone[timezone] || errors.add(:timezone, "is not recognized")
  end
end
