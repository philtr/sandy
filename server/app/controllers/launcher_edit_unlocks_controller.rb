class LauncherEditUnlocksController < ApplicationController
  before_action :require_authentication
  before_action :require_parent_profile

  def create
    device = current_family.devices.not_revoked.find(params[:device_id])
    unlocked_until = device.unlock_launcher_edit!(parent_profile: current_parent_profile)
    redirect_to root_path, notice: "App editing unlocked for #{device.name} until #{helpers.l(unlocked_until, format: :short)}."
  rescue Device::InactiveTimerError
    redirect_to root_path, alert: "Add screen time before unlocking app editing for #{device.name}."
  end

  def destroy
    device = current_family.devices.not_revoked.find(params[:device_id])
    device.lock_launcher_edit!(parent_profile: current_parent_profile)
    redirect_to root_path, notice: "App editing locked for #{device.name}."
  end
end
