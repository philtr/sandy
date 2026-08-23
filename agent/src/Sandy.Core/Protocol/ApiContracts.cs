using System.Text.Json.Serialization;

namespace Sandy.Core.Protocol;

public sealed record EnrollmentRequest(
    [property: JsonPropertyName("join_code")] string JoinCode,
    [property: JsonPropertyName("device_name")] string DeviceName,
    [property: JsonPropertyName("agent_version")] string AgentVersion,
    [property: JsonPropertyName("platform")] string Platform = "windows");

public sealed record EnrollmentResponse(
    [property: JsonPropertyName("device_id")] long DeviceId,
    [property: JsonPropertyName("device_token")] string DeviceToken,
    [property: JsonPropertyName("timer_state")] TimerSnapshot TimerState);

public sealed record HeartbeatRequest(
    [property: JsonPropertyName("agent_version")] string AgentVersion,
    [property: JsonPropertyName("overlay_active")] bool OverlayActive);

public sealed record DeviceEvent(
    [property: JsonPropertyName("event_id")] Guid EventId,
    [property: JsonPropertyName("event_type")] string EventType,
    [property: JsonPropertyName("occurred_at")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("metadata")] IReadOnlyDictionary<string, object?>? Metadata = null);

public sealed record DeviceEventBatch(
    [property: JsonPropertyName("events")] IReadOnlyList<DeviceEvent> Events);
