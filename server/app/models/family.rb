class Family < ApplicationRecord
  has_one :account, dependent: :destroy
  has_many :parent_profiles, dependent: :destroy
  has_many :devices, dependent: :destroy

  has_secure_password :enrollment_code, validations: false

  validates :name, :timezone, :enrollment_code_digest, presence: true
  validate :timezone_must_exist

  def self.generate_join_code
    alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"
    Array.new(3) { Array.new(4) { alphabet[SecureRandom.random_number(alphabet.length)] }.join }.join("-")
  end

  def authenticate_join_code(code)
    authenticate_enrollment_code(self.class.normalize_join_code(code))
  end

  def self.normalize_join_code(code)
    code.to_s.upcase.gsub(/[^A-Z0-9]/, "")
  end

  private

  def timezone_must_exist
    ActiveSupport::TimeZone[timezone] || errors.add(:timezone, "is not recognized")
  end
end
