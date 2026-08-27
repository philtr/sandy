require "test_helper"

class DeviceHistoryTest < ActionDispatch::IntegrationTest
  setup do
    @family = create_family
    @account = create_account(@family)
    @profile = @family.parent_profiles.create!(name: "Alex")
    @device = @family.devices.create!(name: "Gaming PC")
    post session_path, params: { email: @account.email, password: "correct-horse" }
  end

  test "device page previews the ten newest records and links to complete histories" do
    create_history(11)

    get device_path(@device)

    assert_response :success
    newest_grant = @device.time_grants.order(created_at: :desc, id: :desc).first
    oldest_grant = @device.time_grants.order(created_at: :desc, id: :desc).last
    assert_select "#grants-list p", count: 10
    assert_select "#events-list p", count: 10
    assert_select ".diagnostic-list .diagnostic-entry", count: 10
    assert_select "#grants-list p[data-record-id='#{newest_grant.id}']", count: 1
    assert_select "#grants-list p[data-record-id='#{oldest_grant.id}']", count: 0
    assert_select "#events-list", text: /Event 11/
    assert_select "#events-list", text: /Event 1(?:\D|$)/, count: 0
    assert_select ".diagnostic-list", text: /Diagnostic 11/
    assert_select ".diagnostic-list", text: /Diagnostic 1(?:\D|$)/, count: 0
    assert_select "#grants-title + .panel-actions .panel-kicker", text: "11 grants"
    assert_select "#events-title + .panel-actions .panel-kicker", text: "11 events"
    assert_select "a[href='#{device_time_grants_path(@device)}']", text: "View all"
    assert_select "a[href='#{device_events_path(@device)}']", text: "View all"
    assert_select "a[href='#{device_diagnostics_path(@device)}']", text: "View all"
  end

  test "complete histories paginate one hundred records at a time" do
    create_history(101)
    oldest_grant = @device.time_grants.order(created_at: :desc, id: :desc).last

    get device_time_grants_path(@device)
    assert_response :success
    assert_select "h1", text: "Time grants"
    assert_select "#grants-list p", count: 100
    assert_select "nav.pagination[aria-label='Time grants pages']" do
      assert_select "a[href='#{device_time_grants_path(@device, page: 2)}']", text: "Next"
    end

    get device_time_grants_path(@device, page: 2)
    assert_select "#grants-list p[data-record-id='#{oldest_grant.id}']", count: 1
    assert_select "a[href='#{device_time_grants_path(@device, page: 1)}']", text: "Previous"

    get device_events_path(@device, page: 2)
    assert_response :success
    assert_select "h1", text: "Events"
    assert_select "#events-list p", count: 1, text: /Event 1(?:\D|$)/

    get device_diagnostics_path(@device, page: 2)
    assert_response :success
    assert_select "h1", text: "Agent diagnostics"
    assert_select ".diagnostic-list .diagnostic-entry", count: 1, text: /Diagnostic 1(?:\D|$)/
  end

  test "history pages cannot access another family's device" do
    other_family = Family.new(name: "Other", timezone: "UTC")
    other_family.enrollment_code = "OTHERFAMILY"
    other_family.save!
    other_device = other_family.devices.create!(name: "Other PC")

    get device_events_path(other_device)

    assert_response :not_found
  end

  private

  def create_history(count)
    count.times do |index|
      number = index + 1
      timestamp = (count - number).minutes.ago
      @device.time_grants.create!(
        parent_profile: @profile,
        duration_seconds: 5.minutes.to_i,
        resulting_expires_at: timestamp + 5.minutes,
        idempotency_key: "Grant #{number}",
        created_at: timestamp
      )
      @device.device_events.create!(
        event_id: "event-#{number}",
        kind: "event_#{number}",
        occurred_at: timestamp
      )
      @device.device_events.create!(
        event_id: "diagnostic-#{number}",
        kind: DeviceEvent::AGENT_DIAGNOSTIC_KIND,
        occurred_at: timestamp,
        details: {
          severity: "info",
          component: "sync",
          code: "diagnostic_#{number}",
          message: "Diagnostic #{number}"
        }
      )
    end
  end
end
