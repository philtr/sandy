require "test_helper"
require "open3"

class Sandy::DemoDataTest < ActiveSupport::TestCase
  test "reads standalone demo credentials from the environment" do
    script = <<~RUBY
      require "sandy/demo_data"
      puts Sandy::DemoData::EMAIL
      puts Sandy::DemoData::PASSWORD
    RUBY
    environment = {
      "SANDY_DEMO_EMAIL" => "parents@sandy.test",
      "SANDY_DEMO_PASSWORD" => "custom-password"
    }

    output, status = Open3.capture2e(
      environment,
      RbConfig.ruby,
      "-I#{Rails.root.join('lib')}",
      "-e",
      script
    )

    assert status.success?, output
    assert_equal [ "parents@sandy.test", "custom-password" ], output.lines(chomp: true)
  end

  test "loads the deterministic family, credentials, profiles, and showcase devices" do
    now = Time.zone.parse("2026-08-25 12:00:00")

    family = Sandy::DemoData.load!(now:)

    assert_equal "Sandy Demo Family", family.name
    assert_equal %w[Alex Sam], family.parent_profiles.order(:created_at).pluck(:name)
    assert_equal Sandy::DemoData::EMAIL, family.account.email
    assert family.account.authenticate(Sandy::DemoData::PASSWORD)
    assert_equal 4, family.devices.count

    devices = family.devices.index_by(&:name)
    active = devices.fetch(Sandy::DemoData::DEVICES.dig(:active, :name))
    expired = devices.fetch(Sandy::DemoData::DEVICES.dig(:expired, :name))
    offline = devices.fetch(Sandy::DemoData::DEVICES.dig(:offline, :name))
    revoked = devices.fetch(Sandy::DemoData::DEVICES.dig(:revoked, :name))

    assert_equal "active", active.dashboard_status(at: now)
    assert_equal "expired", expired.dashboard_status(at: now)
    assert_equal "offline", offline.dashboard_status(at: now)
    assert_equal "revoked", revoked.dashboard_status(at: now)
    assert_equal active, Device.authenticate_token(Sandy::DemoData::DEVICES.dig(:active, :token))
    assert_equal expired, Device.authenticate_token(Sandy::DemoData::DEVICES.dig(:expired, :token))
    assert_equal offline, Device.authenticate_token(Sandy::DemoData::DEVICES.dig(:offline, :token))
    assert Device.revoked_token?(Sandy::DemoData::DEVICES.dig(:revoked, :token))
    assert expired.overlay_active?
    assert_nil revoked.token_digest
  end

  test "short demo credential does not weaken normal account password validation" do
    Sandy::DemoData.load!

    account = Account.new(email: "another@example.test", password: Sandy::DemoData::PASSWORD)

    assert_not account.valid?
    assert_includes account.errors[:password], "is too short (minimum is 10 characters)"
    assert Account.find_by!(email: Sandy::DemoData::EMAIL).authenticate(Sandy::DemoData::PASSWORD)
  end

  test "loads representative, attributed activity and can be rerun" do
    now = Time.zone.parse("2026-08-25 12:00:00")

    Sandy::DemoData.load!(now:)
    family = Sandy::DemoData.load!(now:)

    assert_equal 1, Family.count
    assert_equal 1, TimeGrant.count
    assert_equal 4, DeviceEvent.count
    assert_equal %w[device_revoked launcher_edit_unlocked overlay_shown time_granted],
      DeviceEvent.order(:kind).pluck(:kind)
    assert_equal "Alex", DeviceEvent.find_by!(kind: "time_granted").details.fetch("parent_profile")
    assert_equal "Sam", DeviceEvent.find_by!(kind: "launcher_edit_unlocked").details.fetch("parent_profile")
    assert_equal 4, family.devices.count
  end
end
