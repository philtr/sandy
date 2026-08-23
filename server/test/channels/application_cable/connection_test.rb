require "test_helper"

class ApplicationCable::ConnectionTest < ActionCable::Connection::TestCase
  test "connects an enrolled device with a bearer header" do
    device = create_family.devices.create!(name: "Gaming PC")
    token = device.issue_token!

    connect headers: { "Authorization" => "Bearer #{token}" }

    assert_equal device, connection.current_device
    assert_nil connection.current_account
  end

  test "rejects an unknown bearer token" do
    assert_reject_connection { connect headers: { "Authorization" => "Bearer unknown" } }
  end
end
