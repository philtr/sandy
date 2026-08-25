require "test_helper"
require "rake"

Rake::Task.define_task(:environment) unless Rake::Task.task_defined?(:environment)
Rake.application.rake_require("demo", [ Rails.root.join("lib/tasks").to_s ])

class DemoTaskTest < ActiveSupport::TestCase
  setup do
    @task = Rake::Task["demo:seed"]
    @task.reenable
  end

  test "refuses to load demo data into the test database even when explicitly enabled" do
    existing_family = create_family
    previous_flag = ENV["SANDY_DEMO"]
    ENV["SANDY_DEMO"] = "1"

    output = capture_io do
      error = assert_raises(SystemExit) { @task.invoke }
      assert_equal 1, error.status
    end

    assert_match(/Refusing to seed demo data/, output.second)
    assert_equal existing_family, Family.find(existing_family.id)
  ensure
    ENV["SANDY_DEMO"] = previous_flag
  end
end
