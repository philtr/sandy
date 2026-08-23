class DeviceEvent < ApplicationRecord
  belongs_to :device

  validates :event_id, :kind, :occurred_at, presence: true
  validates :event_id, uniqueness: { scope: :device_id }, length: { maximum: 100 }
  validates :kind, length: { maximum: 60 }
end
