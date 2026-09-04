module Api
  module V1
    class BaseController < ActionController::API
      before_action :authenticate_device!

      private

      attr_reader :current_device

      def authenticate_device!
        token = request.authorization.to_s.match(/\ABearer (.+)\z/i)&.captures&.first
        @current_device = Device.authenticate_token(token)
        return if @current_device

        revoked_device = Device.revoked_token_device(token)
        if revoked_device
          if legacy_release_allowed?(revoked_device.family)
            return render json: revoked_device.family.revoked_device_release_snapshot(
              state_version: revoked_device.state_version
            )
          end
          return render json: { error: "device_revoked" }, status: :forbidden
        end

        family = legacy_revoked_release_family(token)
        return render json: family.revoked_device_release_snapshot if family

        render json: { error: "unauthorized" }, status: :unauthorized
      end

      def legacy_revoked_release_family(token)
        return if token.blank? || !legacy_agent? || !timer_endpoint?

        # Agent 1.1 cannot understand device_revoked. Recovery mode releases an
        # older credential even when its device record is gone.
        Family.find_by(allow_revoked_devices: true)
      end

      def legacy_release_allowed?(family)
        family.allow_revoked_devices? && legacy_agent? && timer_endpoint?
      end

      def legacy_agent?
        version = request.user_agent.to_s[/Sandy-Agent\/(\d+\.\d+\.\d+)/, 1]
        version.present? && Gem::Version.new(version) < Gem::Version.new("2.0.0")
      rescue ArgumentError
        false
      end

      def timer_endpoint?
        %w[states heartbeats].include?(controller_name)
      end
    end
  end
end
