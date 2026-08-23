class DashboardController < ApplicationController
  before_action :require_authentication

  def show
    @profiles = current_family.parent_profiles.order(:created_at)
    @devices = current_family.devices.order(:name)
    @recent_grants = TimeGrant.includes(:device, :parent_profile).where(device: @devices).order(created_at: :desc).limit(20)
    @recent_events = DeviceEvent.includes(:device).where(device: @devices).order(occurred_at: :desc).limit(20)
  end
end
