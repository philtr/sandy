class DashboardController < ApplicationController
  before_action :require_authentication

  def show
    @profiles = current_family.parent_profiles.order(:created_at)
    @devices = current_family.devices.order(:name)
    @recent_events = DeviceEvent.eager_load(:device).where(device: @devices).order(occurred_at: :desc, id: :desc).limit(20).to_a
  end
end
