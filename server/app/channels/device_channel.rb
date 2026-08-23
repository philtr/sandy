class DeviceChannel < ApplicationCable::Channel
  def subscribed
    reject unless current_device
    stream_for current_device
    transmit(type: "timer_state", timer_state: current_device.timer_snapshot)
  end
end
