class ApplicationJob < ActiveJob::Base
  # Retry jobs that encounter a deadlock.
  # retry_on ActiveRecord::Deadlocked

  # Most jobs are safe to discard when their records no longer exist.
  # discard_on ActiveJob::DeserializationError
end
