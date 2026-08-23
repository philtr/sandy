module ApplicationCable
  class Connection < ActionCable::Connection::Base
    identified_by :current_device, :current_account

    def connect
      self.current_device = Device.authenticate_token(bearer_token)
      self.current_account = Account.find_by(id: request.session[:account_id])
      reject_unauthorized_connection unless current_device || current_account
    end

    private

    def bearer_token
      request.headers["Authorization"].to_s.match(/\ABearer (.+)\z/i)&.captures&.first
    end
  end
end
