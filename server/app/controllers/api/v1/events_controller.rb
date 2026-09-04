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
              event_type = attributes.fetch("event_type")
              raise ArgumentError, "unknown event type: #{event_type}" unless DeviceEvent::AGENT_EVENT_KINDS.include?(event_type)

              current_device.device_events.create!(
                event_id: attributes.fetch("event_id"),
                kind: event_type,
                occurred_at: Time.iso8601(attributes.fetch("occurred_at")),
                details: attributes.fetch("metadata", {}).to_h
              )
              accepted += 1
            rescue ActiveRecord::RecordNotUnique
              # A retry means this event was already accepted.
            end
          end
          prune_agent_diagnostics!
        end
        render json: { accepted: accepted, received: events.size }, status: :created
      rescue ActionController::ParameterMissing, KeyError, ArgumentError, ActiveRecord::RecordInvalid => error
        render json: { error: "invalid_events", detail: error.message }, status: :unprocessable_content
      end

      private

      def prune_agent_diagnostics!
        expired_ids = current_device.device_events.agent_diagnostics
          .order(occurred_at: :desc, id: :desc)
          .offset(DeviceEvent::AGENT_DIAGNOSTIC_RETENTION)
          .pluck(:id)
        current_device.device_events.where(id: expired_ids).delete_all if expired_ids.any?
      end
    end
  end
end
