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
