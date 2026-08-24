class EnrollmentCodesController < ApplicationController
  before_action :require_authentication
  before_action :require_parent_profile

  def show
    @join_code = flash[:join_code]
    render @join_code.present? ? :show : :unavailable
  end

  def update
    @join_code = Family.generate_join_code
    current_family.update!(enrollment_code: Family.normalize_join_code(@join_code))
    redirect_to enrollment_code_path, status: :see_other, flash: { join_code: @join_code }
  end
end
