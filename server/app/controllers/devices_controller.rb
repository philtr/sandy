class DevicesController < ApplicationController
  before_action :require_authentication

  def show
    @device = current_family.devices.find(params[:id])
    @grants = @device.time_grants.includes(:parent_profile).order(created_at: :desc).limit(100)
    @events = @device.device_events.order(occurred_at: :desc).limit(100)
  end
end
