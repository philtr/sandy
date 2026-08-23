class SessionsController < ApplicationController
  rate_limit to: 10, within: 1.minute, only: :create

  def new
    redirect_to new_setup_path unless Family.exists?
  end

  def create
    account = Account.find_by(email: params[:email].to_s.strip.downcase)
    if account&.authenticate(params[:password])
      reset_session
      session[:account_id] = account.id
      redirect_to root_path
    else
      flash.now[:alert] = "Email or password is incorrect."
      render :new, status: :unprocessable_content
    end
  end

  def destroy
    reset_session
    redirect_to new_session_path, notice: "Signed out."
  end
end
