require "test_helper"
require "rake"

Rake::Task.define_task(:environment) unless Rake::Task.task_defined?(:environment)
Rake.application.rake_require("time_grant_events", [ Rails.root.join("lib/tasks").to_s ])

class TimeGrantEventsTaskTest < ActiveSupport::TestCase
  setup do
    @task = Rake::Task["sandy:backfill_time_grant_events"]
    @task.reenable
  end

  test "backfills historical grants and is safe to rerun" do
    family = create_family
    profile = family.parent_profiles.create!(name: "Alex")
    device = family.devices.create!(name: "Gaming PC")
    granted_at = Time.zone.parse("2026-08-20 09:30:00")
    grant = TimeGrant.create!(
      device:,
      parent_profile: profile,
      duration_seconds: 30.minutes,
      resulting_expires_at: granted_at + 30.minutes,
      idempotency_key: "historical",
      created_at: granted_at,
      updated_at: granted_at
    )

    first_output = capture_io { @task.invoke }.first
    @task.reenable
    second_output = capture_io { @task.invoke }.first

    assert_equal 1, device.device_events.where(event_id: grant.device_event_id).count
    assert_match(/1 created, 0 already present/, first_output)
    assert_match(/0 created, 1 already present/, second_output)
  end
end
