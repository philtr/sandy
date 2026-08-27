class DevicesController < ApplicationController
  HISTORY_PREVIEW_SIZE = 10

  before_action :require_authentication
  before_action :require_parent_profile, only: [ :destroy, :archive ]

  def show
    @device = current_family.devices.find(params[:id])
    grants = @device.time_grants.includes(:parent_profile).order(created_at: :desc, id: :desc)
    events = @device.device_events.where.not(kind: DeviceEvent::AGENT_DIAGNOSTIC_KIND).order(occurred_at: :desc, id: :desc)
    diagnostics = @device.device_events.agent_diagnostics.order(occurred_at: :desc, id: :desc)
    @grant_count = grants.count
    @event_count = events.count
    @diagnostic_count = diagnostics.count
    @grants = grants.limit(HISTORY_PREVIEW_SIZE)
    @events = events.limit(HISTORY_PREVIEW_SIZE)
    @diagnostics = diagnostics.limit(HISTORY_PREVIEW_SIZE)
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
    redirect_to root_path, notice: "Unenrolled #{device.name}. Agent 2.0 requires the current join code."
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
