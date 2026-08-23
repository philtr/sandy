class SetupsController < ApplicationController
  before_action :ensure_setup_available

  def new
  end

  def create
    unless valid_setup_token?(params[:setup_token])
      flash.now[:alert] = "The setup token is invalid."
      return render :new, status: :unauthorized
    end

    join_code = Family.generate_join_code
    Family.transaction do
      family = Family.create!(
        name: params[:family_name],
        timezone: params[:timezone],
        enrollment_code: Family.normalize_join_code(join_code)
      )
      account = family.create_account!(
        email: params[:email],
        password: params[:password],
        password_confirmation: params[:password_confirmation]
      )
      family.parent_profiles.create!([ { name: params[:parent_one_name] }, { name: params[:parent_two_name] } ])
      session[:account_id] = account.id
    end

    @join_code = join_code
    render :created, status: :created
  rescue ActiveRecord::RecordInvalid => error
    flash.now[:alert] = error.record.errors.full_messages.to_sentence
    render :new, status: :unprocessable_content
  end

  private

  def ensure_setup_available
    redirect_to root_path, alert: "Setup is already complete." if Family.exists?
  end

  def valid_setup_token?(candidate)
    expected = ENV["SETUP_TOKEN"].to_s
    return false if expected.blank? || candidate.blank?

    ActiveSupport::SecurityUtils.secure_compare(
      Digest::SHA256.hexdigest(candidate.to_s),
      Digest::SHA256.hexdigest(expected)
    )
  end
end
