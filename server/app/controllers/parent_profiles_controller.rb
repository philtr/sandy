class ParentProfilesController < ApplicationController
  before_action :require_authentication

  def update
    profile = current_family.parent_profiles.find(params[:id])
    cookies.signed.permanent[:parent_profile_id] = { value: profile.id, httponly: true, same_site: :lax }
    redirect_back fallback_location: root_path, notice: "Using this phone as #{profile.name}."
  end

  def destroy
    cookies.delete(:parent_profile_id)
    redirect_to root_path
  end
end
