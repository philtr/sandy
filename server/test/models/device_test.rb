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

  test "family allow setting returns a schema-one active snapshot for revoked devices" do
    family = create_family
    family.update!(allow_revoked_devices: true)
    device = family.devices.create!(name: "Gaming PC", expires_at: 10.minutes.from_now)
    token = device.issue_token!

    device.revoke!

    assert_equal device, Device.authenticate_token(token)
    assert device.token_digest.present?
    snapshot = device.timer_snapshot
    assert_equal 1, snapshot[:schema_version]
    assert_equal "active", snapshot[:timer_status]
    assert snapshot[:expires_at].present?
    assert_operator snapshot[:remaining_seconds], :>, 0
  end

  test "only revoked devices can be archived" do
    device = create_family.devices.create!(name: "Gaming PC")

    assert_raises(ActiveRecord::RecordInvalid) { device.archive! }
    device.update!(revoked_at: Time.current)
    assert device.archive!
    assert device.archived_at?
  end

  test "screen time revocation expires the deadline and is idempotent" do
    family = create_family
    profile = family.parent_profiles.create!(name: "Alex")
    now = Time.zone.parse("2026-08-23 12:00:00")
    previous_deadline = now + 30.minutes
    device = family.devices.create!(name: "Gaming PC", expires_at: previous_deadline)

    event = device.revoke_screen_time!(parent_profile: profile, idempotency_key: "revoke-1", now:)
    repeated = device.revoke_screen_time!(parent_profile: profile, idempotency_key: "revoke-1", now: now + 1.minute)

    assert_equal event.id, repeated.id
    assert_equal now, device.reload.expires_at
    assert_equal 1, device.state_version
    assert_equal 1, device.device_events.where(kind: "screen_time_revoked").count
    assert_equal profile.name, event.details["parent_profile"]
    assert_equal previous_deadline.iso8601(3), event.details["previous_expires_at"]
  end
end
