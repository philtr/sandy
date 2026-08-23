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

    post device_time_grants_path(@device), params: { duration_seconds: 15.minutes.to_i, idempotency_key: "phone-submit-1" }
    assert_redirected_to root_path
    assert_equal 1, @device.time_grants.count
    assert_equal @profile, @device.time_grants.first.parent_profile
    assert @device.reload.expires_at.future?
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
end
