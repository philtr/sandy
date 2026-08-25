namespace :demo do
  desc "Load the isolated local demo database"
  task seed: :environment do
    database = ActiveRecord::Base.connection_db_config.database.to_s
    demo_database = File.basename(database) == "demo.sqlite3"

    unless Rails.env.development? && ENV["SANDY_DEMO"] == "1" && demo_database
      abort "Refusing to seed demo data outside development's explicitly enabled demo.sqlite3 database"
    end

    family = Sandy::DemoData.load!
    puts "Loaded #{family.name} (#{Sandy::DemoData::EMAIL} / #{Sandy::DemoData::PASSWORD})"
  end
end
