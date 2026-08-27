class DeviceEventsController < ApplicationController
  PAGE_SIZE = 100

  before_action :require_authentication

  def index
    @device = current_family.devices.find(params[:device_id])
    events = @device.device_events
      .where.not(kind: DeviceEvent::AGENT_DIAGNOSTIC_KIND)
      .order(occurred_at: :desc, id: :desc)
    @events, @page, @total_pages, @total_count = paginate(events, per_page: PAGE_SIZE)
  end
end
