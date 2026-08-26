require "test_helper"

class SessionsTest < ActionDispatch::IntegrationTest
  test "a successful sign-in issues a persistent parent session cookie" do
    family = create_family
    create_account(family)

    post session_path, params: { email: "parents@example.test", password: "correct-horse" }

    assert_redirected_to root_path
    session_cookie = response.headers.fetch("Set-Cookie")
    assert_match(/_server_session=/, session_cookie)
    assert_match(/expires=/i, session_cookie)
    assert_match(/httponly/i, session_cookie)
    assert_match(/samesite=lax/i, session_cookie)
  end

  test "parent sessions are retained for thirty days" do
    options = Rails.application.config.session_options

    assert_equal "_server_session", options[:key]
    assert_equal 30.days, options[:expire_after]
    assert_equal true, options[:httponly]
    assert_equal :lax, options[:same_site]
  end
end
