require "test_helper"

class DeviceApiTest < ActionDispatch::IntegrationTest
  setup do
    @family = create_family
  end

  test "agent enrolls and reconciles through state and heartbeat" do
    post api_v1_enrollments_path, params: {
      join_code: "abcd-efgh-jklm", device_name: "Gaming PC", platform: "windows", agent_version: "1.0.0"
    }, as: :json

    assert_response :created
    enrollment = response.parsed_body
    assert enrollment["device_token"].present?
    assert_equal "expired", enrollment.dig("timer_state", "timer_status")

    headers = { "Authorization" => "Bearer #{enrollment['device_token']}" }
    get api_v1_state_path, headers:, as: :json
    assert_response :success
    assert_equal enrollment["device_id"], Device.authenticate_token(enrollment["device_token"]).id

    post api_v1_heartbeats_path, params: { agent_version: "1.0.1", overlay_active: true }, headers:, as: :json
    assert_response :success
    device = Device.find(enrollment["device_id"])
    assert device.last_heartbeat_at.present?
    assert device.overlay_active?
    assert_equal "1.0.1", device.agent_version
  end

  test "invalid enrollment and unauthenticated state are rejected" do
    post api_v1_enrollments_path, params: { join_code: "wrong", device_name: "PC" }, as: :json
    assert_response :unauthorized

    get api_v1_state_path, as: :json
    assert_response :unauthorized
  end

  test "revoked device receives a 1.1-compatible active state when allowed in family settings" do
    @family.update!(allow_revoked_devices: true)
    device = @family.devices.create!(name: "Retired PC", expires_at: 30.minutes.from_now)
    token = device.issue_token!
    device.revoke!
    headers = { "Authorization" => "Bearer #{token}" }

    get api_v1_state_path, headers:, as: :json

    assert_response :success
    assert_equal 1, response.parsed_body["schema_version"]
    assert_equal "active", response.parsed_body["timer_status"]
    assert response.parsed_body["expires_at"].present?

    post api_v1_heartbeats_path, params: { agent_version: "1.1.0", overlay_active: false }, headers:, as: :json
    assert_response :success
    assert_equal "active", response.parsed_body["timer_status"]
  end

  test "legacy revoked device with an erased digest receives emergency release on timer endpoints" do
    device = @family.devices.create!(name: "Locked PC", expires_at: 30.minutes.ago)
    old_token = device.issue_token!
    device.revoke!
    device.update_column(:token_digest, nil)
    @family.update!(allow_revoked_devices: true)
    headers = { "Authorization" => "Bearer #{old_token}" }

    get api_v1_state_path, headers:, as: :json
    assert_response :success
    assert_equal 1, response.parsed_body["schema_version"]
    assert_equal "active", response.parsed_body["timer_status"]
    assert_operator response.parsed_body["state_version"], :>, device.state_version

    post api_v1_heartbeats_path, params: { agent_version: "1.1.0", overlay_active: true }, headers:, as: :json
    assert_response :success
    assert_equal "active", response.parsed_body["timer_status"]

    post api_v1_events_path, params: { events: [] }, headers:, as: :json
    assert_response :unauthorized

    device.archive!
    get api_v1_state_path, headers:, as: :json
    assert_response :unauthorized
  end

  test "event batch is idempotent" do
    device = @family.devices.create!(name: "Gaming PC")
    token = device.issue_token!
    headers = { "Authorization" => "Bearer #{token}" }
    body = { events: [ { event_id: "event-1", event_type: "overlay_shown", occurred_at: Time.current.iso8601, metadata: { monitor_count: 2 } } ] }

    2.times do
      post api_v1_events_path, params: body, headers:, as: :json
      assert_response :created
    end

    assert_equal 1, device.device_events.count
    assert_equal "overlay_shown", device.device_events.first.kind
  end
end
