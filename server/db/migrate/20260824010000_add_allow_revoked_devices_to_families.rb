class AddAllowRevokedDevicesToFamilies < ActiveRecord::Migration[8.1]
  def change
    add_column :families, :allow_revoked_devices, :boolean, null: false, default: false
  end
end
