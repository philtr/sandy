# frozen_string_literal: true

require "test_helper"
require "stringio"

require Rails.root.join("lib/sandy/demo_agent")

class Sandy::DemoAgentTest < ActiveSupport::TestCase
  class FakeTransport
    attr_reader :calls

    def initialize(*results)
      @results = results
      @calls = []
    end

    def heartbeat(token:, payload:)
      @calls << { token:, payload: }
      result = @results.shift || raise("no fake response configured")
      raise result if result.is_a?(Exception)

      result
    end
  end

  test "heartbeats both demo identities with bearer credentials and metadata" do
    transport = FakeTransport.new(active_snapshot, expired_snapshot)
    agent = build_agent(transport:)

    agent.heartbeat_all

    expected_tokens = Sandy::DemoData::DEVICES.values_at(:active, :expired).pluck(:token)
    assert_equal expected_tokens, transport.calls.pluck(:token)
    assert_equal [ false, true ], transport.calls.pluck(:payload).pluck(:overlay_active)
    expected_names = Sandy::DemoData::DEVICES.values_at(:active, :expired).pluck(:name)
    assert_equal expected_names, transport.calls.pluck(:payload).pluck(:metadata).pluck(:machine_name)
    assert transport.calls.all? { |call| call.dig(:payload, :agent_version) == Sandy::DemoData::AGENT_VERSION }
  end

  test "derives the next heartbeat overlay state from each timer response" do
    transport = FakeTransport.new(expired_snapshot, active_snapshot, active_snapshot, expired_snapshot)
    agent = build_agent(transport:)

    2.times { agent.heartbeat_all }

    second_cycle = transport.calls.last(2)
    assert_equal [ true, false ], second_cycle.pluck(:payload).pluck(:overlay_active)
    assert_equal [ false, true ], agent.devices.pluck(:overlay_active)
  end

  test "keeps processing identities after a transient failure without printing tokens" do
    output = StringIO.new
    secret = Sandy::DemoData::DEVICES.dig(:active, :token)
    transport = FakeTransport.new(
      Sandy::DemoAgent::TransientError.new("request included #{secret}"),
      expired_snapshot
    )
    agent = build_agent(transport:, output:)

    agent.heartbeat_all

    assert_equal 2, transport.calls.size
    assert_includes output.string, "retrying next cycle"
    refute_includes output.string, secret
  end

  test "run heartbeats immediately and then waits thirty seconds" do
    stop_run = Class.new(StandardError)
    waits = []
    transport = FakeTransport.new(active_snapshot, expired_snapshot)
    sleeper = lambda do |seconds|
      waits << seconds
      raise stop_run
    end
    agent = build_agent(transport:, sleeper:)

    assert_raises(stop_run) { agent.run }

    assert_equal 2, transport.calls.size
    assert_equal [ 30 ], waits
  end

  test "authentication failures abort the agent and do not heartbeat later identities" do
    transport = FakeTransport.new(Sandy::DemoAgent::AuthenticationError.new("rejected"), active_snapshot)
    agent = build_agent(transport:)

    assert_raises(Sandy::DemoAgent::AuthenticationError) { agent.heartbeat_all }
    assert_equal 1, transport.calls.size
  end

  private

  def build_agent(transport:, sleeper: ->(_) { }, output: StringIO.new)
    Sandy::DemoAgent.new(base_url: "http://example.test", transport:, sleeper:, output:)
  end

  def active_snapshot
    { "timer_status" => "active", "heartbeat_interval_seconds" => 30 }
  end

  def expired_snapshot
    { "timer_status" => "expired", "heartbeat_interval_seconds" => 30 }
  end
end

class Sandy::DemoAgentNetHttpTransportTest < ActiveSupport::TestCase
  FakeResponse = Data.define(:code, :body)

  test "sends JSON heartbeat with bearer authorization" do
    response = FakeResponse.new(code: "200", body: JSON.generate("timer_status" => "active"))
    request = capture_http_request(response:) do |transport|
      transport.heartbeat(token: "secret-token", payload: { overlay_active: false })
    end

    assert_equal "Bearer secret-token", request["Authorization"]
    assert_equal "application/json", request["Content-Type"]
    assert_equal "Sandy-Agent/#{Sandy::DemoData::AGENT_VERSION}", request["User-Agent"]
    assert_equal({ "overlay_active" => false }, JSON.parse(request.body))
  end

  test "maps unauthorized and forbidden responses to authentication failures" do
    [ "401", "403" ].each do |status|
      response = FakeResponse.new(code: status, body: "{}")

      assert_raises(Sandy::DemoAgent::AuthenticationError) do
        capture_http_request(response:) { |transport| transport.heartbeat(token: "secret", payload: {}) }
      end
    end
  end

  test "maps server and network failures to retryable failures" do
    response = FakeResponse.new(code: "503", body: "{}")
    assert_raises(Sandy::DemoAgent::TransientError) do
      capture_http_request(response:) { |transport| transport.heartbeat(token: "secret", payload: {}) }
    end

    failing_http = ->(*) { raise Errno::ECONNREFUSED }
    transport = Sandy::DemoAgent::NetHttpTransport.new(base_url: "http://example.test", http_start: failing_http)
    assert_raises(Sandy::DemoAgent::TransientError) { transport.heartbeat(token: "secret", payload: {}) }
  end

  private

  def capture_http_request(response:)
    request = nil
    fake_http = Object.new
    fake_http.define_singleton_method(:request) do |value|
      request = value
      response
    end

    http_start = ->(*args, **options, &block) { block.call(fake_http) }
    transport = Sandy::DemoAgent::NetHttpTransport.new(base_url: "http://example.test/root", http_start:)
    yield transport

    request
  end
end
