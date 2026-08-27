require "test_helper"

class DeviceDiagnosticsTest < ActionDispatch::IntegrationTest
  setup do
    @family = create_family
    @account = create_account(@family)
    @device = @family.devices.create!(name: "Gaming PC", agent_version: "2.2.2")
    @token = @device.issue_token!
    @headers = { "Authorization" => "Bearer #{@token}" }
  end

  test "agent uploads a structured diagnostic" do
    post api_v1_events_path, params: { events: [ diagnostic_payload ] }, headers: @headers, as: :json

    assert_response :created
    diagnostic = @device.device_events.find_by!(event_id: "diagnostic-1")
    assert_equal "agent_diagnostic", diagnostic.kind
    assert_equal "error", diagnostic.details["severity"]
    assert_equal "audio", diagnostic.details["component"]
    assert_equal "cue_playback_failed", diagnostic.details["code"]
  end

  test "event API rejects unknown event types and oversized metadata" do
    post api_v1_events_path, params: {
      events: [ diagnostic_payload.merge(event_id: "unknown-1", event_type: "arbitrary_log") ]
    }, headers: @headers, as: :json

    assert_response :unprocessable_content
    assert_equal "invalid_events", response.parsed_body["error"]

    oversized = diagnostic_payload.deep_dup
    oversized[:event_id] = "oversized-1"
    oversized[:metadata][:message] = "x" * 5_000
    post api_v1_events_path, params: { events: [ oversized ] }, headers: @headers, as: :json

    assert_response :unprocessable_content
    assert_equal "invalid_events", response.parsed_body["error"]
  end

  test "server retains only the newest diagnostics for a device" do
    250.times do |index|
      @device.device_events.create!(
        event_id: "old-diagnostic-#{index}",
        kind: "agent_diagnostic",
        occurred_at: (300 - index).minutes.ago,
        details: diagnostic_payload.fetch(:metadata)
      )
    end

    post api_v1_events_path, params: {
      events: [ diagnostic_payload.merge(event_id: "new-diagnostic", occurred_at: Time.current.iso8601) ]
    }, headers: @headers, as: :json

    assert_response :created
    diagnostics = @device.device_events.where(kind: "agent_diagnostic")
    assert_equal 250, diagnostics.count
    assert diagnostics.exists?(event_id: "new-diagnostic")
    refute diagnostics.exists?(event_id: "old-diagnostic-0")
  end

  test "parent can view and copy diagnostics separately from ordinary events" do
    @device.device_events.create!(
      event_id: "ordinary-1",
      kind: "warning_shown",
      occurred_at: 2.minutes.ago,
      details: { minutes: 5 }
    )
    @device.device_events.create!(
      event_id: "diagnostic-ui-1",
      kind: "agent_diagnostic",
      occurred_at: 1.minute.ago,
      details: diagnostic_payload.fetch(:metadata)
    )
    post session_path, params: { email: @account.email, password: "correct-horse" }

    get device_path(@device)

    assert_response :success
    assert_select "section.diagnostics[data-controller='clipboard']" do
      assert_select "h2", text: "Agent diagnostics"
      assert_select "button[data-action='clipboard#copy'][data-clipboard-target='button']", text: "Copy diagnostics"
      assert_select "pre[data-clipboard-target='source']", text: /ERROR audio cue_playback_failed/
      assert_select ".diagnostic-entry[data-severity='error']", text: /Could not start the screen-time cue/
    end
    assert_select "#events-list" do
      assert_select "p", text: /Warning shown/
      assert_select "p", text: /Agent diagnostic/, count: 0
    end
  end

  test "diagnostics do not crowd ordinary dashboard activity" do
    profile = @family.parent_profiles.create!(name: "Alex")
    @device.device_events.create!(
      event_id: "dashboard-ordinary",
      kind: "warning_shown",
      occurred_at: 2.minutes.ago,
      details: { minutes: 5 }
    )
    @device.device_events.create!(
      event_id: "dashboard-diagnostic",
      kind: "agent_diagnostic",
      occurred_at: 1.minute.ago,
      details: diagnostic_payload.fetch(:metadata)
    )
    post session_path, params: { email: @account.email, password: "correct-horse" }
    patch parent_profile_path, params: { id: profile.id }

    get root_path

    assert_response :success
    assert_select ".activity-item[data-kind='warning_shown']", count: 1
    assert_select ".activity-item[data-kind='agent_diagnostic']", count: 0
  end

  private

  def diagnostic_payload
    {
      event_id: "diagnostic-1",
      event_type: "agent_diagnostic",
      occurred_at: Time.current.iso8601,
      metadata: {
        severity: "error",
        component: "audio",
        code: "cue_playback_failed",
        message: "Could not start the screen-time cue.",
        context: { cue: "one-minute.wav", backend: "SoundPlayer" },
        exception: { type: "InvalidOperationException", hresult: "0x80131509" }
      }
    }
  end
end
