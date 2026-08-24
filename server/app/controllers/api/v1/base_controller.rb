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

        family = legacy_revoked_release_family(token)
        return render json: family.revoked_device_release_snapshot if family

        render json: { error: "unauthorized" }, status: :unauthorized
      end

      def legacy_revoked_release_family(token)
        return if token.blank? || !%w[states heartbeats].include?(controller_name)

        # Older server releases erased the token digest on revoke, making the
        # locked agent impossible to identify. This deliberately narrow fallback
        # is enabled by the parent UI and ends when the legacy record is archived.
        Family.where(allow_revoked_devices: true).find do |family|
          family.devices.revoked.not_archived.where(token_digest: nil).exists?
        end
      end
    end
  end
end
