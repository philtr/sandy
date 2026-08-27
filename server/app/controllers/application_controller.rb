class ApplicationController < ActionController::Base
  # Only allow modern browsers supporting webp images, web push, badges, import maps, CSS nesting, and CSS :has.
  allow_browser versions: :modern

  # Changes to the importmap will invalidate the etag for HTML responses
  stale_when_importmap_changes

  helper_method :current_account, :current_family, :current_parent_profile

  private

  def current_account
    @current_account ||= Account.find_by(id: session[:account_id])
  end

  def current_family
    current_account&.family
  end

  def current_parent_profile
    return unless current_family

    @current_parent_profile ||= current_family.parent_profiles.find_by(id: cookies.signed[:parent_profile_id])
  end

  def require_authentication
    return if current_account

    redirect_to new_session_path, alert: "Please sign in."
  end

  def require_parent_profile
    return if current_parent_profile

    redirect_to root_path, alert: "Choose who is using this phone before granting time."
  end

  def paginate(scope, per_page:)
    total_count = scope.count
    total_pages = [ (total_count.to_f / per_page).ceil, 1 ].max
    page = params[:page].to_i.clamp(1, total_pages)
    records = scope.offset((page - 1) * per_page).limit(per_page)

    [ records, page, total_pages, total_count ]
  end
end
