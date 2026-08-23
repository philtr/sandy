require "test_helper"

class SetupFlowTest < ActionDispatch::IntegrationTest
  test "setup token creates the only family and shows a one-time join code" do
    previous = ENV["SETUP_TOKEN"]
    ENV["SETUP_TOKEN"] = "deployment-secret"

    post setup_path, params: {
      setup_token: "deployment-secret",
      family_name: "River Family",
      timezone: "Central Time (US & Canada)",
      email: "family@example.test",
      password: "long-password",
      password_confirmation: "long-password",
      parent_one_name: "Alex",
      parent_two_name: "Sam"
    }

    assert_response :created
    assert_includes response.body, "Save the PC join code"
    assert_equal 1, Family.count
    assert_equal 2, Family.first.parent_profiles.count

    get new_setup_path
    assert_redirected_to root_path
  ensure
    ENV["SETUP_TOKEN"] = previous
  end
end
