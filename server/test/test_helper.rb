ENV["RAILS_ENV"] ||= "test"
require_relative "../config/environment"
require "rails/test_help"

module ActiveSupport
  class TestCase
    parallelize(workers: 1)

    # Setup all fixtures in test/fixtures/*.yml for all tests in alphabetical order.
    fixtures :all

    # Add more helper methods to be used by all tests here...
  end
end

module SandyTestData
  def create_family
    family = Family.new(name: "Test Family", timezone: "Central Time (US & Canada)")
    family.enrollment_code = Family.normalize_join_code("ABCD-EFGH-JKLM")
    family.save!
    family
  end

  def create_account(family)
    family.create_account!(email: "parents@example.test", password: "correct-horse", password_confirmation: "correct-horse")
  end
end

ActiveSupport::TestCase.include SandyTestData
ActionDispatch::IntegrationTest.include SandyTestData
