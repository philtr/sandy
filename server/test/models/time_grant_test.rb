require "test_helper"

class TimeGrantTest < ActiveSupport::TestCase
  test "grant starts an allowance window and extensions preserve its start" do
    family = create_family
    profile = family.parent_profiles.create!(name: "Alex")
    device = family.devices.create!(name: "Gaming PC")
    now = Time.zone.parse("2026-08-24 12:00:00")

    TimeGrant.grant!(device:, parent_profile: profile, duration_seconds: 15.minutes, idempotency_key: "first", now:)
    TimeGrant.grant!(device:, parent_profile: profile, duration_seconds: 5.minutes, idempotency_key: "second", now: now + 2.minutes)

    assert_equal now, device.reload.allowance_started_at
    assert_equal now + 20.minutes, device.expires_at
  end

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

    first_event = device.device_events.find_by!(event_id: first.device_event_id)
    assert_equal "time_granted", first_event.kind
    assert_equal first.created_at, first_event.occurred_at
    assert_equal first.id, first_event.details["time_grant_id"]
    assert_equal profile.name, first_event.details["parent_profile"]
    assert_equal 15.minutes.to_i, first_event.details["duration_seconds"]
    assert_equal first.resulting_expires_at.iso8601(3), first_event.details["resulting_expires_at"]
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
    assert_equal 1, device.device_events.where(kind: "time_granted").count
  end

  test "grant and timer changes roll back when its activity event cannot be created" do
    family = create_family
    profile = family.parent_profiles.create!(name: "Alex")
    device = family.devices.create!(name: "Gaming PC")
    next_grant_id = TimeGrant.maximum(:id).to_i + 1
    device.device_events.create!(
      event_id: "time-grant:#{next_grant_id}",
      kind: "agent_event",
      occurred_at: Time.current
    )

    assert_raises ActiveRecord::RecordInvalid do
      TimeGrant.grant!(device:, parent_profile: profile, duration_seconds: 15.minutes, idempotency_key: "conflict")
    end

    assert_empty device.time_grants
    assert_nil device.reload.expires_at
    assert_equal 0, device.state_version
  end

  test "historical grants can backfill their event idempotently" do
    family = create_family
    profile = family.parent_profiles.create!(name: "Alex")
    device = family.devices.create!(name: "Gaming PC")
    granted_at = Time.zone.parse("2026-08-20 09:30:00")
    grant = TimeGrant.create!(
      device:,
      parent_profile: profile,
      duration_seconds: 30.minutes,
      previous_expires_at: nil,
      resulting_expires_at: granted_at + 30.minutes,
      idempotency_key: "historical",
      created_at: granted_at,
      updated_at: granted_at
    )

    original = grant.ensure_device_event!
    repeated = grant.ensure_device_event!

    assert_equal original.id, repeated.id
    assert_equal granted_at, original.occurred_at
    assert_equal 1, device.device_events.where(event_id: grant.device_event_id).count
  end
end
