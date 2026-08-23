class TimeGrantsController < ApplicationController
  before_action :require_authentication
  before_action :require_parent_profile

  def create
    device = current_family.devices.not_revoked.find(params[:device_id])
    grant = TimeGrant.grant!(
      device: device,
      parent_profile: current_parent_profile,
      duration_seconds: params.require(:duration_seconds).to_i,
      idempotency_key: params[:idempotency_key].presence || request.request_id
    )

    respond_to do |format|
      format.html { redirect_to root_path, notice: "Added #{grant.duration_seconds / 60} minutes to #{device.name}." }
      format.json { render json: { time_grant_id: grant.id, timer_state: device.reload.timer_snapshot }, status: :created }
    end
  rescue ActiveRecord::RecordInvalid => error
    respond_to do |format|
      format.html { redirect_to root_path, alert: error.record.errors.full_messages.to_sentence }
      format.json { render json: { error: "invalid_grant", detail: error.record.errors.full_messages.to_sentence }, status: :unprocessable_content }
    end
  end
end
