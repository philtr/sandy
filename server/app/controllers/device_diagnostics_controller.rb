class DeviceDiagnosticsController < ApplicationController
  PAGE_SIZE = 100

  before_action :require_authentication

  def index
    @device = current_family.devices.find(params[:device_id])
    diagnostics = @device.device_events.agent_diagnostics.order(occurred_at: :desc, id: :desc)
    @diagnostics, @page, @total_pages, @total_count = paginate(diagnostics, per_page: PAGE_SIZE)
  end
end
