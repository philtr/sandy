# This file is auto-generated from the current state of the database. Instead
# of editing this file, please use the migrations feature of Active Record to
# incrementally modify your database, and then regenerate this schema definition.
#
# This file is the source Rails uses to define your schema when running `bin/rails
# db:schema:load`. When creating a new database, `bin/rails db:schema:load` tends to
# be faster and is potentially less error prone than running all of your
# migrations from scratch. Old migrations may fail to apply correctly if those
# migrations use external dependencies or application code.
#
# It's strongly recommended that you check this file into your version control system.

ActiveRecord::Schema[8.1].define(version: 2026_08_24_020000) do
  create_table "accounts", force: :cascade do |t|
    t.datetime "created_at", null: false
    t.string "email", null: false
    t.integer "family_id", null: false
    t.string "password_digest", null: false
    t.datetime "updated_at", null: false
    t.index "lower(email)", name: "index_accounts_on_lower_email", unique: true
    t.index ["family_id"], name: "index_accounts_on_family_id", unique: true
  end

  create_table "device_events", force: :cascade do |t|
    t.datetime "created_at", null: false
    t.json "details", default: {}, null: false
    t.integer "device_id", null: false
    t.string "event_id", null: false
    t.string "kind", null: false
    t.datetime "occurred_at", null: false
    t.datetime "updated_at", null: false
    t.index ["device_id", "event_id"], name: "index_device_events_on_device_id_and_event_id", unique: true
    t.index ["device_id", "occurred_at"], name: "index_device_events_on_device_id_and_occurred_at"
    t.index ["device_id"], name: "index_device_events_on_device_id"
  end

  create_table "devices", force: :cascade do |t|
    t.string "agent_version"
    t.datetime "archived_at"
    t.datetime "allowance_started_at"
    t.datetime "created_at", null: false
    t.datetime "expires_at"
    t.integer "family_id", null: false
    t.datetime "last_heartbeat_at"
    t.datetime "launcher_edit_unlocked_until"
    t.json "metadata", default: {}, null: false
    t.string "name", null: false
    t.boolean "overlay_active", default: false, null: false
    t.string "platform", default: "windows", null: false
    t.datetime "revoked_at"
    t.string "revoked_token_digest"
    t.integer "state_version", default: 0, null: false
    t.string "token_digest"
    t.datetime "updated_at", null: false
    t.index ["family_id", "archived_at"], name: "index_devices_on_family_id_and_archived_at"
    t.index ["family_id"], name: "index_devices_on_family_id"
    t.index ["revoked_token_digest"], name: "index_devices_on_revoked_token_digest", unique: true
    t.index ["token_digest"], name: "index_devices_on_token_digest", unique: true
  end

  create_table "families", force: :cascade do |t|
    t.boolean "allow_revoked_devices", default: false, null: false
    t.datetime "created_at", null: false
    t.string "enrollment_code_digest", null: false
    t.string "name", null: false
    t.string "timezone", default: "UTC", null: false
    t.datetime "updated_at", null: false
  end

  create_table "parent_profiles", force: :cascade do |t|
    t.datetime "created_at", null: false
    t.integer "family_id", null: false
    t.string "name", null: false
    t.datetime "updated_at", null: false
    t.index ["family_id"], name: "index_parent_profiles_on_family_id"
  end

  create_table "time_grants", force: :cascade do |t|
    t.datetime "created_at", null: false
    t.integer "device_id", null: false
    t.integer "duration_seconds", null: false
    t.string "idempotency_key", null: false
    t.integer "parent_profile_id", null: false
    t.datetime "previous_expires_at"
    t.datetime "resulting_expires_at", null: false
    t.datetime "updated_at", null: false
    t.index ["device_id", "idempotency_key"], name: "index_time_grants_on_device_id_and_idempotency_key", unique: true
    t.index ["device_id"], name: "index_time_grants_on_device_id"
    t.index ["parent_profile_id"], name: "index_time_grants_on_parent_profile_id"
  end

  add_foreign_key "accounts", "families"
  add_foreign_key "device_events", "devices"
  add_foreign_key "devices", "families"
  add_foreign_key "parent_profiles", "families"
  add_foreign_key "time_grants", "devices"
  add_foreign_key "time_grants", "parent_profiles"
end
