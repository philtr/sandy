module Api
  module V1
    class EnrollmentsController < ActionController::API
      rate_limit to: 10, within: 1.minute, only: :create

      def create
        family = Family.first
        unless family&.authenticate_join_code(params[:join_code])
          return render json: { error: "invalid_join_code" }, status: :unauthorized
        end

        device = family.devices.create!(
          name: params.require(:device_name),
          platform: params[:platform].presence || "windows",
          agent_version: params[:agent_version]
        )
        token = device.issue_token!

        render json: { device_id: device.id, device_token: token, timer_state: device.timer_snapshot }, status: :created
      rescue ActionController::ParameterMissing, ActiveRecord::RecordInvalid => error
        render json: { error: "invalid_enrollment", detail: error.message }, status: :unprocessable_content
      end
    end
  end
end
