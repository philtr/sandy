module Api
  module V1
    class StatesController < BaseController
      def show
        render json: current_device.timer_snapshot
      end
    end
  end
end
