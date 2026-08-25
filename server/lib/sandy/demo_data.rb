require "active_support/core_ext/object/blank"

module Sandy
  module DemoData
    EMAIL = ENV["SANDY_DEMO_EMAIL"].presence || "demo@sandy.test"
    PASSWORD = ENV["SANDY_DEMO_PASSWORD"].presence || "password"
    AGENT_VERSION = "2.0.0-demo"
    HEARTBEAT_INTERVAL_SECONDS = 30

    DEVICES = {
      active: {
        name: "Homework PC",
        token: "sandy-demo-homework-pc-token-v1",
        timer_status: "active",
        overlay_active: false
      }.freeze,
      expired: {
        name: "Gaming PC",
        token: "sandy-demo-gaming-pc-token-v1",
        timer_status: "expired",
        overlay_active: true
      }.freeze,
      offline: {
        name: "Family Laptop",
        token: "sandy-demo-family-laptop-token-v1",
        timer_status: "active",
        overlay_active: false
      }.freeze,
      revoked: {
        name: "Old PC",
        token: "sandy-demo-old-pc-token-v1",
        timer_status: "expired",
        overlay_active: false
      }.freeze
    }.freeze

    module_function

    def load!(now: Time.current)
      now = now.change(usec: 0)

      ApplicationRecord.transaction do
        clear_existing_data!

        family = Family.create!(
          name: "Sandy Demo Family",
          timezone: "Central Time (US & Canada)",
          enrollment_code: Family.normalize_join_code("DEMO-SAND-Y123")
        )
        family.create_account!(email: EMAIL, password_digest: BCrypt::Password.create(PASSWORD))

        alex = family.parent_profiles.create!(name: "Alex")
        sam = family.parent_profiles.create!(name: "Sam")

        devices = {
          active: create_active_device!(family, now:),
          expired: create_expired_device!(family, now:),
          offline: create_offline_device!(family, now:),
          revoked: create_revoked_device!(family, now:)
        }

        create_activity!(devices:, alex:, sam:, now:)
        family
      end
    end

    def create_active_device!(family, now:)
      data = DEVICES.fetch(:active)
      family.devices.create!(
        name: data.fetch(:name),
        platform: "windows",
        agent_version: AGENT_VERSION,
        token_digest: Device.digest_token(data.fetch(:token)),
        allowance_started_at: now - 15.minutes,
        expires_at: now + 45.minutes,
        last_heartbeat_at: now - 10.seconds,
        overlay_active: data.fetch(:overlay_active),
        state_version: 3,
        metadata: { "os_version" => "Windows 11", "machine_name" => "HOMEWORK-PC" }
      )
    end

    def create_expired_device!(family, now:)
      data = DEVICES.fetch(:expired)
      family.devices.create!(
        name: data.fetch(:name),
        platform: "windows",
        agent_version: AGENT_VERSION,
        token_digest: Device.digest_token(data.fetch(:token)),
        allowance_started_at: now - 1.hour,
        expires_at: now - 12.minutes,
        last_heartbeat_at: now - 8.seconds,
        overlay_active: data.fetch(:overlay_active),
        state_version: 4,
        metadata: { "os_version" => "Windows 11", "machine_name" => "GAMING-PC" }
      )
    end

    def create_offline_device!(family, now:)
      data = DEVICES.fetch(:offline)
      family.devices.create!(
        name: data.fetch(:name),
        platform: "windows",
        agent_version: "1.1.0",
        token_digest: Device.digest_token(data.fetch(:token)),
        allowance_started_at: now - 30.minutes,
        expires_at: now + 30.minutes,
        last_heartbeat_at: now - 2.hours,
        overlay_active: data.fetch(:overlay_active),
        state_version: 2,
        metadata: { "os_version" => "Windows 10", "machine_name" => "FAMILY-LAPTOP" }
      )
    end

    def create_revoked_device!(family, now:)
      data = DEVICES.fetch(:revoked)
      family.devices.create!(
        name: data.fetch(:name),
        platform: "windows",
        agent_version: "1.1.0",
        expires_at: now - 1.day,
        last_heartbeat_at: now - 1.day,
        overlay_active: data.fetch(:overlay_active),
        revoked_at: now - 1.day,
        revoked_token_digest: Device.digest_token(data.fetch(:token)),
        state_version: 2,
        metadata: { "os_version" => "Windows 10", "machine_name" => "OLD-PC" }
      )
    end

    def create_activity!(devices:, alex:, sam:, now:)
      grant = TimeGrant.create!(
        device: devices.fetch(:active),
        parent_profile: alex,
        duration_seconds: 30.minutes.to_i,
        previous_expires_at: now + 15.minutes,
        resulting_expires_at: now + 45.minutes,
        idempotency_key: "demo-alex-homework-grant",
        created_at: now - 10.minutes,
        updated_at: now - 10.minutes
      )
      grant.ensure_device_event!

      devices.fetch(:expired).device_events.create!(
        event_id: "demo-overlay-shown",
        kind: "overlay_shown",
        occurred_at: now - 12.minutes,
        details: { "monitor_count" => 2 }
      )
      devices.fetch(:offline).device_events.create!(
        event_id: "demo-launcher-edit-unlocked",
        kind: "launcher_edit_unlocked",
        occurred_at: now - 2.hours,
        details: {
          "parent_profile_id" => sam.id,
          "parent_profile" => sam.name,
          "unlocked_until" => (now - 90.minutes).iso8601(3)
        }
      )
      devices.fetch(:revoked).device_events.create!(
        event_id: "demo-device-revoked",
        kind: "device_revoked",
        occurred_at: now - 1.day,
        details: { "parent_profile_id" => alex.id, "parent_profile" => alex.name }
      )
    end

    def clear_existing_data!
      DeviceEvent.delete_all
      TimeGrant.delete_all
      Device.delete_all
      ParentProfile.delete_all
      Account.delete_all
      Family.delete_all
    end

    private_class_method :create_active_device!, :create_expired_device!, :create_offline_device!,
      :create_revoked_device!, :create_activity!, :clear_existing_data!
  end
end
