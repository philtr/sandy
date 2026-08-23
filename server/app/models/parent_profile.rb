class ParentProfile < ApplicationRecord
  belongs_to :family
  has_many :time_grants, dependent: :restrict_with_error

  validates :name, presence: true, length: { maximum: 60 }
end
