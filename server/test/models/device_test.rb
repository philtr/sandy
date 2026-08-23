require "test_helper"

class DeviceTest < ActiveSupport::TestCase
  test "snapshot is versioned and uses an absolute deadline" do
    family = create_family
    device = family.devices.create!(name: "Gaming PC", expires_at: Time.zone.parse("2026-08-23 12:30:00"), state_version: 4)
    snapshot = device.timer_snapshot(at: Time.zone.parse("2026-08-23 12:00:00"))

    assert_equal 1, snapshot[:schema_version]
    assert_equal 4, snapshot[:state_version]
    assert_equal 1800, snapshot[:remaining_seconds]
    assert_equal "active", snapshot[:timer_status]
    assert_equal 30, snapshot[:heartbeat_interval_seconds]
  end

  test "token authentication rejects revoked devices" do
    device = create_family.devices.create!(name: "Gaming PC")
    token = device.issue_token!
    assert_equal device, Device.authenticate_token(token)

    device.update!(revoked_at: Time.current)
    assert_nil Device.authenticate_token(token)
  end
end
