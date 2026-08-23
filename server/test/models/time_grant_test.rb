require "test_helper"

class TimeGrantTest < ActiveSupport::TestCase
  test "grants extend from now when expired and accumulate when active" do
    family = create_family
    profile = family.parent_profiles.create!(name: "Alex")
    now = Time.zone.parse("2026-08-23 12:00:00")
    device = family.devices.create!(name: "Gaming PC", expires_at: now - 1.hour)

    first = TimeGrant.grant!(device:, parent_profile: profile, duration_seconds: 15.minutes, idempotency_key: "one", now:)
    second = TimeGrant.grant!(device:, parent_profile: profile, duration_seconds: 30.minutes, idempotency_key: "two", now:)

    assert_equal now + 15.minutes, first.resulting_expires_at
    assert_equal now + 45.minutes, second.resulting_expires_at
    assert_equal 2, device.reload.state_version
  end

  test "a repeated idempotency key returns the original grant without extending time" do
    family = create_family
    profile = family.parent_profiles.create!(name: "Alex")
    device = family.devices.create!(name: "Gaming PC")
    now = Time.current

    original = TimeGrant.grant!(device:, parent_profile: profile, duration_seconds: 15.minutes, idempotency_key: "retry", now:)
    repeated = TimeGrant.grant!(device:, parent_profile: profile, duration_seconds: 60.minutes, idempotency_key: "retry", now: now + 1.minute)

    assert_equal original.id, repeated.id
    assert_equal original.resulting_expires_at, device.reload.expires_at
    assert_equal 1, device.state_version
  end
end
