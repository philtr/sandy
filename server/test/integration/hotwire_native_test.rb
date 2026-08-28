require "test_helper"

class HotwireNativeTest < ActionDispatch::IntegrationTest
  HOTWIRE_NATIVE_HEADERS = {
    "HTTP_USER_AGENT" => "Sandy/1.0 Hotwire Native iOS; Turbo Native iOS;"
  }.freeze

  setup do
    @family = create_family
    @account = create_account(@family)
    @profile = @family.parent_profiles.create!(name: "Alex")
  end

  test "native requests use native chrome while browser requests keep PWA chrome" do
    get new_session_path, headers: HOTWIRE_NATIVE_HEADERS

    assert_response :success
    assert_select "body.hotwire-native"
    assert_select ".site-header", count: 0
    assert_select ".skip-link", count: 0

    get new_session_path

    assert_response :success
    assert_select "body:not(.hotwire-native)"
    assert_select ".site-header", count: 1
    assert_select ".skip-link", count: 1
  end

  test "native dashboard exposes settings and sign out without browser header" do
    sign_in_and_select_profile

    get root_path, headers: HOTWIRE_NATIVE_HEADERS

    assert_response :success
    assert_select ".native-account-tools" do
      assert_select "a[href='#{settings_path}']", text: "Family Settings"
      assert_select "form[action='#{session_path}'] button", text: "Sign Out"
    end
  end

  test "native settings link proposes the native server configuration screen" do
    sign_in_and_select_profile

    get settings_path, headers: HOTWIRE_NATIVE_HEADERS

    assert_response :success
    assert_select ".back-link", count: 0
    assert_select ".native-connection-tools" do
      assert_select "a[href='#{settings_path(native_screen: "server")}']", text: "Change Sandy Server"
    end
  end

  test "browser pages do not expose native-only controls" do
    sign_in_and_select_profile

    get root_path
    assert_select ".native-account-tools", count: 0

    get settings_path
    assert_select ".native-connection-tools", count: 0
    assert_select ".back-link", count: 1
  end

  test "versioned iOS path configuration is valid and complete" do
    get "/configurations/ios_v1.json"

    assert_response :success
    configuration = JSON.parse(response.body)
    assert_equal({}, configuration.fetch("settings"))

    rules = configuration.fetch("rules")
    assert rules.any? { |rule| rule.fetch("properties")["pull_to_refresh_enabled"] == true }
    assert rules.any? { |rule| rule.fetch("properties")["presentation"] == "replace_root" && rule.fetch("patterns").include?("^/$") }
    assert rules.any? { |rule| rule.fetch("properties")["presentation"] == "replace_root" && rule.fetch("patterns").include?("^/session/new$") }
    assert rules.any? { |rule| rule.fetch("properties")["view_controller"] == "server_configuration" }
  end

  private

  def sign_in_and_select_profile
    post session_path, params: { email: @account.email, password: "correct-horse" }
    patch parent_profile_path, params: { id: @profile.id }
  end
end
