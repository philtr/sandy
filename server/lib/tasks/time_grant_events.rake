namespace :sandy do
  desc "Backfill DeviceEvent records for historical time grants"
  task backfill_time_grant_events: :environment do
    created = 0
    existing = 0

    TimeGrant.includes(:device, :parent_profile).find_each do |grant|
      if grant.device.device_events.exists?(event_id: grant.device_event_id)
        grant.ensure_device_event!
        existing += 1
      else
        grant.ensure_device_event!
        created += 1
      end
    end

    puts "Time grant event backfill complete: #{created} created, #{existing} already present."
  end
end
