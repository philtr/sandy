class FamilyChannel < ApplicationCable::Channel
  def subscribed
    reject unless current_account
    stream_for current_account.family
  end
end
