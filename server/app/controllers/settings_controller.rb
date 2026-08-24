class SettingsController < ApplicationController
  before_action :require_authentication
  before_action :require_parent_profile, only: :update

  def show
    @revoked_devices = current_family.devices.revoked.not_archived.order(revoked_at: :desc)
    @archived_devices = current_family.devices.where.not(archived_at: nil).order(archived_at: :desc)
  end

  def update
    allow_revoked_devices = ActiveModel::Type::Boolean.new.cast(params[:allow_revoked_devices])
    current_family.update!(allow_revoked_devices:)

    if allow_revoked_devices
      current_family.devices.revoked.where.not(token_digest: nil).find_each(&:broadcast_timer_state!)
      notice = "Revoked PCs will now be released from Sandy enforcement."
    else
      current_family.devices.revoked.where.not(token_digest: nil).find_each do |device|
        ActionCable.server.remote_connections.where(current_device: device, current_account: nil).disconnect
      end
      notice = "Revoked PCs now require re-enrollment."
    end

    redirect_to settings_path, notice:
  end
end
