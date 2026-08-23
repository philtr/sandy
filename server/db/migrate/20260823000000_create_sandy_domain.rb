class CreateSandyDomain < ActiveRecord::Migration[8.1]
  def change
    create_table :families do |t|
      t.string :name, null: false
      t.string :timezone, null: false, default: "UTC"
      t.string :enrollment_code_digest, null: false
      t.timestamps
    end

    create_table :accounts do |t|
      t.references :family, null: false, foreign_key: true, index: { unique: true }
      t.string :email, null: false
      t.string :password_digest, null: false
      t.timestamps
    end
    add_index :accounts, "lower(email)", unique: true, name: "index_accounts_on_lower_email"

    create_table :parent_profiles do |t|
      t.references :family, null: false, foreign_key: true
      t.string :name, null: false
      t.timestamps
    end

    create_table :devices do |t|
      t.references :family, null: false, foreign_key: true
      t.string :name, null: false
      t.string :platform, null: false, default: "windows"
      t.string :agent_version
      t.string :token_digest
      t.datetime :expires_at
      t.integer :state_version, null: false, default: 0
      t.datetime :last_heartbeat_at
      t.boolean :overlay_active, null: false, default: false
      t.datetime :revoked_at
      t.json :metadata, null: false, default: {}
      t.timestamps
    end
    add_index :devices, :token_digest, unique: true

    create_table :time_grants do |t|
      t.references :device, null: false, foreign_key: true
      t.references :parent_profile, null: false, foreign_key: true
      t.integer :duration_seconds, null: false
      t.datetime :previous_expires_at
      t.datetime :resulting_expires_at, null: false
      t.string :idempotency_key, null: false
      t.timestamps
    end
    add_index :time_grants, [ :device_id, :idempotency_key ], unique: true

    create_table :device_events do |t|
      t.references :device, null: false, foreign_key: true
      t.string :event_id, null: false
      t.string :kind, null: false
      t.datetime :occurred_at, null: false
      t.json :details, null: false, default: {}
      t.timestamps
    end
    add_index :device_events, [ :device_id, :event_id ], unique: true
    add_index :device_events, [ :device_id, :occurred_at ]
  end
end
