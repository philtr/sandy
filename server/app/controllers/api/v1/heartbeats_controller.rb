module Api
  module V1
    class HeartbeatsController < BaseController
      def create
        current_device.record_heartbeat!(
          agent_version: params[:agent_version],
          overlay_active: params[:overlay_active],
          metadata: params[:metadata].respond_to?(:to_unsafe_h) ? params[:metadata].to_unsafe_h : {}
        )
        render json: current_device.timer_snapshot
      end
    end
  end
end
