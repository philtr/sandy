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

        family = revoked_release_family(token)
        return render json: family.revoked_device_release_snapshot if family

        render json: { error: "unauthorized" }, status: :unauthorized
      end

      def revoked_release_family(token)
        return if token.blank? || !%w[states heartbeats].include?(controller_name)

        # Recovery mode deliberately does not identify the device. This lets an
        # agent whose token digest was erased by an older revoke receive a release
        # snapshot even if its old dashboard record was archived or removed.
        Family.find_by(allow_revoked_devices: true)
      end
    end
  end
end
