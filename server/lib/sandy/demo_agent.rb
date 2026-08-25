# frozen_string_literal: true

require "json"
require "net/http"
require "uri"

require_relative "demo_data"

module Sandy
  class DemoAgent
    class Error < StandardError; end
    class AuthenticationError < Error; end
    class TransientError < Error; end
    class ProtocolError < Error; end

    Device = Struct.new(:name, :token, :overlay_active, keyword_init: true)

    class NetHttpTransport
      OPEN_TIMEOUT_SECONDS = 5
      READ_TIMEOUT_SECONDS = 10

      def initialize(base_url:, http_start: Net::HTTP.method(:start))
        @heartbeat_uri = URI.join(ensure_trailing_slash(base_url), "api/v1/heartbeats")
        @http_start = http_start
      end

      def heartbeat(token:, payload:)
        request = Net::HTTP::Post.new(@heartbeat_uri)
        request["Authorization"] = "Bearer #{token}"
        request["Content-Type"] = "application/json"
        request["Accept"] = "application/json"
        request["User-Agent"] = "Sandy-Agent/#{DemoData::AGENT_VERSION}"
        request.body = JSON.generate(payload)

        response = @http_start.call(
          @heartbeat_uri.host,
          @heartbeat_uri.port,
          use_ssl: @heartbeat_uri.scheme == "https",
          open_timeout: OPEN_TIMEOUT_SECONDS,
          read_timeout: READ_TIMEOUT_SECONDS
        ) { |http| http.request(request) }

        case response.code.to_i
        when 200..299
          parse_snapshot(response.body)
        when 401, 403
          raise AuthenticationError, "demo device credential was rejected"
        when 500..599
          raise TransientError, "demo server returned #{response.code}"
        else
          raise ProtocolError, "unexpected heartbeat response (HTTP #{response.code})"
        end
      rescue AuthenticationError, ProtocolError, TransientError
        raise
      rescue JSON::ParserError
        raise ProtocolError, "heartbeat response was not valid JSON"
      rescue IOError, EOFError, SocketError, SystemCallError, Timeout::Error => error
        raise TransientError, "heartbeat request failed (#{error.class})"
      end

      private

      def ensure_trailing_slash(base_url)
        value = base_url.to_s
        value.end_with?("/") ? value : "#{value}/"
      end

      def parse_snapshot(body)
        snapshot = JSON.parse(body)
        return snapshot if snapshot.is_a?(Hash) && %w[active expired].include?(snapshot["timer_status"])

        raise ProtocolError, "heartbeat response did not contain a valid timer snapshot"
      end
    end

    def initialize(
      base_url:,
      device_definitions: DemoData::DEVICES.values_at(:active, :expired),
      transport: nil,
      sleeper: ->(seconds) { sleep(seconds) },
      output: $stdout,
      interval_seconds: DemoData::HEARTBEAT_INTERVAL_SECONDS
    )
      @devices = device_definitions.map do |definition|
        Device.new(
          name: definition.fetch(:name),
          token: definition.fetch(:token),
          overlay_active: definition.fetch(:overlay_active, false)
        )
      end
      @transport = transport || NetHttpTransport.new(base_url:)
      @sleeper = sleeper
      @output = output
      @interval_seconds = interval_seconds
    end

    attr_reader :devices

    def run
      loop do
        heartbeat_all
        @sleeper.call(@interval_seconds)
      end
    end

    def heartbeat_all
      devices.each do |device|
        heartbeat(device)
      rescue TransientError => error
        @output.puts "#{device.name}: heartbeat unavailable (#{error.class}); retrying next cycle"
      end
    end

    private

    def heartbeat(device)
      snapshot = @transport.heartbeat(
        token: device.token,
        payload: {
          agent_version: DemoData::AGENT_VERSION,
          overlay_active: device.overlay_active,
          metadata: { os_version: "demo", machine_name: device.name }
        }
      )

      device.overlay_active = snapshot.fetch("timer_status") == "expired"
      @output.puts "#{device.name}: #{snapshot.fetch('timer_status')} (overlay #{device.overlay_active ? 'on' : 'off'})"
      snapshot
    end
  end
end
