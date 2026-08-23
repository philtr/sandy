module Api
  module V1
    class EventsController < BaseController
      def create
        events = params.require(:events)
        return render json: { error: "too_many_events" }, status: :unprocessable_content if events.size > 100

        accepted = 0
        DeviceEvent.transaction do
          events.each do |event|
            attributes = event.respond_to?(:to_unsafe_h) ? event.to_unsafe_h : event.to_h
            begin
              next if current_device.device_events.exists?(event_id: attributes.fetch("event_id"))

              current_device.device_events.create!(
                event_id: attributes.fetch("event_id"),
                kind: attributes.fetch("event_type"),
                occurred_at: Time.iso8601(attributes.fetch("occurred_at")),
                details: attributes.fetch("metadata", {}).to_h
              )
              accepted += 1
            rescue ActiveRecord::RecordNotUnique
              # A retried event is already accepted.
            end
          end
        end
        render json: { accepted: accepted, received: events.size }, status: :created
      rescue ActionController::ParameterMissing, KeyError, ArgumentError, ActiveRecord::RecordInvalid => error
        render json: { error: "invalid_events", detail: error.message }, status: :unprocessable_content
      end
    end
  end
end
