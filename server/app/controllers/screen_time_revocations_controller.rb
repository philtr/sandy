class ScreenTimeRevocationsController < ApplicationController
  before_action :require_authentication
  before_action :require_parent_profile

  def create
    device = current_family.devices.not_revoked.find(params[:device_id])
    event = device.revoke_screen_time!(
      parent_profile: current_parent_profile,
      idempotency_key: params[:idempotency_key].presence || request.request_id
    )

    respond_to do |format|
      format.html { redirect_to root_path, notice: "Revoked screen time for #{device.name}." }
      format.json { render json: { device_event_id: event.id, timer_state: device.reload.timer_snapshot }, status: :created }
    end
  end
end
