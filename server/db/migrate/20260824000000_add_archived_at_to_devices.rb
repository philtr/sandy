class AddArchivedAtToDevices < ActiveRecord::Migration[8.1]
  def change
    add_column :devices, :archived_at, :datetime
    add_index :devices, [ :family_id, :archived_at ]
  end
end
