class EnrollmentCodesController < ApplicationController
  before_action :require_authentication
  before_action :require_parent_profile

  def show
    render :unavailable
  end

  def update
    @join_code = Family.generate_join_code
    current_family.update!(enrollment_code: Family.normalize_join_code(@join_code))
    render :show
  end
end
