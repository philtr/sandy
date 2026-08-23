module Api
  module V1
    class BaseController < ActionController::API
      before_action :authenticate_device!

      private

      attr_reader :current_device

      def authenticate_device!
        token = request.authorization.to_s.match(/\ABearer (.+)\z/i)&.captures&.first
        @current_device = Device.authenticate_token(token)
        render json: { error: "unauthorized" }, status: :unauthorized unless @current_device
      end
    end
  end
end
