class AddLauncherSessionStateToDevices < ActiveRecord::Migration[8.1]
  class MigrationDevice < ActiveRecord::Base
    self.table_name = "devices"
  end

  class MigrationTimeGrant < ActiveRecord::Base
    self.table_name = "time_grants"
  end

  def up
    add_column :devices, :allowance_started_at, :datetime
    add_column :devices, :launcher_edit_unlocked_until, :datetime
    add_column :devices, :revoked_token_digest, :string
    add_index :devices, :revoked_token_digest, unique: true

    MigrationDevice.reset_column_information
    MigrationDevice.where.not(revoked_at: nil).where.not(token_digest: nil).find_each do |device|
      device.update_columns(revoked_token_digest: device.token_digest, token_digest: nil)
    end
    say_with_time "Backfilling reliable active allowance starts" do
      MigrationDevice.where("expires_at > ?", Time.current).find_each do |device|
        started_at = derive_allowance_start(device)
        device.update_columns(allowance_started_at: started_at) if started_at
      end
    end
  end

  def down
    remove_index :devices, :revoked_token_digest
    remove_column :devices, :revoked_token_digest
    remove_column :devices, :launcher_edit_unlocked_until
    remove_column :devices, :allowance_started_at
  end

  private

  def derive_allowance_start(device)
    grants = MigrationTimeGrant.where(device_id: device.id).order(created_at: :desc, id: :desc).to_a
    current = grants.find { |grant| same_second?(grant.resulting_expires_at, device.expires_at) }
    return unless current

    loop do
      previous_deadline = current.previous_expires_at
      return current.created_at if previous_deadline.blank? || previous_deadline <= current.created_at

      prior = grants.find do |grant|
        grant.created_at <= current.created_at && same_second?(grant.resulting_expires_at, previous_deadline)
      end
      return unless prior

      current = prior
    end
  end

  def same_second?(left, right)
    left.present? && right.present? && left.to_i == right.to_i
  end
end
