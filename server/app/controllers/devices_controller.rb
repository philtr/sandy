class DevicesController < ApplicationController
  before_action :require_authentication
  before_action :require_parent_profile, only: [ :destroy, :archive ]

  def show
    @device = current_family.devices.find(params[:id])
    @grants = @device.time_grants.includes(:parent_profile).order(created_at: :desc).limit(100)
    @events = @device.device_events.order(occurred_at: :desc).limit(100)
  end

  def destroy
    device = current_family.devices.not_revoked.find(params[:id])
    device.revoke!
    device.device_events.create!(
      event_id: SecureRandom.uuid,
      kind: "device_revoked",
      occurred_at: Time.current,
      details: { parent_profile: current_parent_profile.name }
    )
    if device.family.allow_revoked_devices?
      device.broadcast_timer_state!
    else
      ActionCable.server.remote_connections.where(current_device: device, current_account: nil).disconnect
    end
    notice = if device.family.allow_revoked_devices?
      "Revoked #{device.name} and released its Sandy screen-time lock."
    else
      "Revoked #{device.name}. Re-enrollment requires the current join code."
    end
    redirect_to root_path, notice:
  end

  def archive
    device = current_family.devices.revoked.not_archived.find(params[:id])
    device.archive!
    device.device_events.create!(
      event_id: SecureRandom.uuid,
      kind: "device_archived",
      occurred_at: Time.current,
      details: { parent_profile: current_parent_profile.name }
    )
    redirect_to settings_path, notice: "Archived #{device.name}."
  end
end
