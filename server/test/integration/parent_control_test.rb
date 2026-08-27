require "test_helper"

class ParentControlTest < ActionDispatch::IntegrationTest
  setup do
    @family = create_family
    @account = create_account(@family)
    @profile = @family.parent_profiles.create!(name: "Alex")
    @device = @family.devices.create!(name: "Gaming PC")
  end

  test "authenticated selected parent can grant time" do
    post session_path, params: { email: @account.email, password: "correct-horse" }
    assert_redirected_to root_path
    patch parent_profile_path, params: { id: @profile.id }
    assert_redirected_to root_path

    post device_time_grants_path(@device), params: { duration_seconds: 5.minutes.to_i, idempotency_key: "phone-submit-1" }
    assert_redirected_to root_path
    assert_equal 1, @device.time_grants.count
    assert_equal @profile, @device.time_grants.first.parent_profile
    assert_equal 5.minutes.to_i, @device.time_grants.first.duration_seconds
    assert_equal 1, @device.device_events.where(kind: "time_granted").count
    assert @device.reload.expires_at.future?
  end

  test "dashboard offers one- and five-minute grants" do
    @device.update!(expires_at: 5.minutes.from_now)
    sign_in_and_select_profile

    get root_path

    assert_response :success
    assert_select "h1", text: "Family PCs"
    assert_select "button", text: "+1 min"
    assert_select "button", text: "+5 min"
    assert_select "button", text: "Unlock app editing for 30 minutes"
  end

  test "selected parent can unlock and lock launcher editing" do
    @device.update!(expires_at: 5.minutes.from_now)
    sign_in_and_select_profile

    post device_launcher_edit_unlock_path(@device)

    assert_redirected_to root_path
    assert @device.reload.launcher_edit_unlocked?
    assert_in_delta 30.minutes.from_now.to_f, @device.launcher_edit_unlocked_until.to_f, 2
    assert_equal 1, @device.state_version
    assert_equal @profile.name, @device.device_events.find_by!(kind: "launcher_edit_unlocked").details["parent_profile"]

    get root_path
    assert_select ".launcher-edit-status", text: /30 minutes remaining/

    delete device_launcher_edit_unlock_path(@device)

    assert_redirected_to root_path
    assert_nil @device.reload.launcher_edit_unlocked_until
    assert_equal 2, @device.state_version
    assert_equal @profile.name, @device.device_events.find_by!(kind: "launcher_edit_locked").details["parent_profile"]
  end

  test "dashboard exposes stable semantic device statuses" do
    active = @family.devices.create!(name: "Active PC", last_heartbeat_at: Time.current, expires_at: 20.minutes.from_now)
    expired = @family.devices.create!(name: "Expired PC", last_heartbeat_at: Time.current, expires_at: 2.minutes.ago)
    revoked = @family.devices.create!(name: "Revoked PC", revoked_at: Time.current)
    archived = @family.devices.create!(name: "Archived PC", revoked_at: 2.days.ago, archived_at: 1.day.ago)
    archived.device_events.create!(event_id: "archived-event", kind: "startup", occurred_at: 1.day.ago)
    sign_in_and_select_profile

    get root_path

    assert_response :success
    assert_select ".device-card[data-status='offline'][data-device-id='#{@device.id}'] .status", text: "offline"
    assert_select ".device-card[data-status='active'][data-device-id='#{active.id}'] .status", text: "active"
    assert_select ".device-card[data-status='expired'][data-device-id='#{expired.id}'] .status", text: "expired"
    assert_select ".device-card[data-status='revoked'][data-device-id='#{revoked.id}'] .status", text: "unenrolled"
    assert_select ".device-card[data-device-id='#{archived.id}']", count: 0
    refute_includes response.body, "Archived PC"
  end

  test "settings gear lists unenrolled PCs and archives them off the dashboard" do
    revoked = @family.devices.create!(name: "Old PC", revoked_at: 2.days.ago)
    sign_in_and_select_profile

    get root_path
    assert_select ".header-actions" do
      assert_select "a.settings-action[href='#{settings_path}']"
      assert_select "form[action='#{session_path}']"
    end
    assert_select ".device-card[data-device-id='#{revoked.id}']", count: 1

    get settings_path
    assert_response :success
    assert_select "h1", text: "Settings"
    assert_select "button", text: "Release legacy unenrolled PCs"
    assert_select "form[action='#{archive_device_path(revoked)}'] button", text: "Archive"

    patch settings_path, params: { allow_revoked_devices: true }
    assert_redirected_to settings_path
    assert @family.reload.allow_revoked_devices?
    get settings_path
    assert_select ".release-setting .status", text: "allow"
    assert_select ".setting-warning", text: /Sandy 1\.x.*any bearer token.*archived or no longer exists/i
    assert_select "button", text: "Stop releasing unenrolled PCs"

    patch archive_device_path(revoked)
    assert_redirected_to settings_path
    assert revoked.reload.archived_at?
    assert_equal "device_archived", revoked.device_events.last.kind

    get root_path
    assert_select ".device-card[data-device-id='#{revoked.id}']", count: 0
  end

  test "selected parent can set the household voice theme for every PC" do
    other_device = @family.devices.create!(name: "Study PC")
    sign_in_and_select_profile

    patch settings_path, params: { voice_theme: "random" }

    assert_redirected_to settings_path
    assert_equal "random", @family.reload.voice_theme
    assert_equal 1, @device.reload.state_version
    assert_equal 1, other_device.reload.state_version
    assert_equal "random", @device.timer_snapshot[:voice_theme]

    get settings_path
    assert_select "select[name='voice_theme'] option[selected][value='random']", text: "Random"
  end

  test "recent activity combines grants and device events in descending event order" do
    sign_in_and_select_profile
    older = @device.device_events.create!(event_id: "older", kind: "startup", occurred_at: 3.hours.ago)
    grant = TimeGrant.grant!(
      device: @device,
      parent_profile: @profile,
      duration_seconds: 5.minutes,
      idempotency_key: "activity-order"
    )
    grant_event = @device.device_events.find_by!(event_id: grant.device_event_id)
    grant_event.update!(occurred_at: 2.hours.ago)
    newer = @device.device_events.create!(event_id: "newer", kind: "warning_shown", occurred_at: 1.hour.ago)

    get root_path

    newer_position = response.body.index("reported warning shown")
    grant_position = response.body.index("added 5 min")
    older_position = response.body.index("reported startup")
    assert newer_position && grant_position && older_position
    assert_operator newer_position, :<, grant_position
    assert_operator grant_position, :<, older_position
    assert_equal [ newer.id, grant_event.id, older.id ], @device.device_events.order(occurred_at: :desc, id: :desc).limit(3).ids
  end

  test "dashboard requires a parent profile before use" do
    post session_path, params: { email: @account.email, password: "correct-horse" }

    get root_path

    assert_response :success
    assert_select ".profile-gate[role='dialog'][aria-modal='true']" do
      assert_select "h1", text: "Who is using this phone?"
      assert_select "button", text: @profile.name
      assert_select "button[autofocus]", count: 1
    end
    assert_select ".dashboard-title", count: 0
    assert_select ".device-grid", count: 0
    assert_select ".history", count: 0
    assert_select "button", text: "+1 min", count: 0
  end

  test "selected profile appears below PCs and can be cleared" do
    sign_in_and_select_profile

    get root_path

    assert_select ".profile-status", text: /Using Sandy as Alex/ do
      assert_select "a[data-turbo-method='delete']", text: "Switch user"
    end
    assert_select ".device-grid + .profile-status"
    assert_select ".history"
    assert_select ".profile-gate", count: 0

    delete parent_profile_path
    assert_redirected_to root_path
    follow_redirect!
    assert_select ".profile-gate"
  end

  test "selected parent can revoke remaining screen time" do
    @device.update!(expires_at: 30.minutes.from_now)
    sign_in_and_select_profile

    get root_path
    assert_select "button[role='switch']", text: /Revoke screen time/

    post device_screen_time_revocation_path(@device), params: { idempotency_key: "revoke-submit-1" }

    assert_redirected_to root_path
    assert_equal "expired", @device.reload.timer_status
    assert_equal 1, @device.state_version
    event = @device.device_events.find_by!(kind: "screen_time_revoked")
    assert_equal @profile.name, event.details["parent_profile"]
  end

  test "family scope prevents access to another family device" do
    other = Family.new(name: "Other", timezone: "UTC")
    other.enrollment_code = "OTHERFAMILY"
    other.save!
    other_device = other.devices.create!(name: "Other PC")
    post session_path, params: { email: @account.email, password: "correct-horse" }

    get device_path(other_device)
    assert_response :not_found
  end

  test "selected parent can generate a join code for adding a PC" do
    sign_in_and_select_profile
    old_digest = @family.enrollment_code_digest

    patch enrollment_code_path

    assert_response :see_other
    assert_redirected_to enrollment_code_path
    assert_not_equal old_digest, @family.reload.enrollment_code_digest
    follow_redirect!
    assert_response :success
    assert_select "h1", text: "Connect a new PC"
    assert_select ".join-code"
  end

  test "add PC actions generate a new join code" do
    sign_in_and_select_profile
    old_digest = @family.enrollment_code_digest

    get enrollment_code_path

    assert_response :success
    assert_select "h1", text: "Connect another PC"
    assert_select "button", text: "Create join code"
    assert_equal old_digest, @family.reload.enrollment_code_digest

    get root_path
    assert_select "h2", text: "Add a PC"
    assert_select "form[action='#{enrollment_code_path}']" do
      assert_select "input[name='_method'][value='patch']"
      assert_select "button", text: "Add PC"
    end
  end

  test "selected parent can unenroll a device" do
    token = @device.issue_token!
    sign_in_and_select_profile

    get device_path(@device)
    assert_select "h2", text: "Unenroll this PC"
    assert_select "button", text: "Unenroll #{@device.name}"

    delete device_path(@device)

    assert_redirected_to root_path
    assert_equal "Unenrolled #{@device.name}. Agent 2.0 requires the current join code.", flash[:notice]
    assert @device.reload.revoked_at?
    assert_nil Device.authenticate_token(token)
    assert Device.revoked_token?(token)
    assert_equal "device_revoked", @device.device_events.last.kind
  end

  private

  def sign_in_and_select_profile
    post session_path, params: { email: @account.email, password: "correct-horse" }
    patch parent_profile_path, params: { id: @profile.id }
  end
end
