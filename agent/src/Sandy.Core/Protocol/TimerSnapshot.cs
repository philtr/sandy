using System.Text.Json.Serialization;

namespace Sandy.Core.Protocol;

public sealed record TimerSnapshot
{
    [JsonPropertyName("schema_version")]
    public required int SchemaVersion { get; init; }

    [JsonPropertyName("state_version")]
    public required long StateVersion { get; init; }

    [JsonPropertyName("server_time")]
    public required DateTimeOffset ServerTime { get; init; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; init; }

    [JsonPropertyName("allowance_started_at")]
    public DateTimeOffset? AllowanceStartedAt { get; init; }

    [JsonPropertyName("launcher_edit_unlocked_until")]
    public DateTimeOffset? LauncherEditUnlockedUntil { get; init; }

    [JsonPropertyName("remaining_seconds")]
    public required long RemainingSeconds { get; init; }

    [JsonPropertyName("timer_status")]
    public required string TimerStatus { get; init; }

    [JsonPropertyName("heartbeat_interval_seconds")]
    public required int HeartbeatIntervalSeconds { get; init; }

    [JsonPropertyName("voice_theme")]
    public string VoiceTheme { get; init; } = "stella";

    public TimerSnapshot Validate()
    {
        if (SchemaVersion != 1)
            throw new ProtocolException($"Unsupported timer schema version {SchemaVersion}.");
        if (StateVersion < 0)
            throw new ProtocolException("State version cannot be negative.");
        if (HeartbeatIntervalSeconds is < 5 or > 300)
            throw new ProtocolException("Heartbeat interval is outside the supported range.");
        if (RemainingSeconds < 0)
            throw new ProtocolException("Remaining seconds cannot be negative.");
        if (TimerStatus is not ("active" or "expired"))
            throw new ProtocolException($"Unknown timer status '{TimerStatus}'.");
        if (TimerStatus == "active" && (ExpiresAt is null || RemainingSeconds == 0))
            throw new ProtocolException("An active snapshot must include a future expiration.");
        if (AllowanceStartedAt is not null && ExpiresAt is not null && AllowanceStartedAt > ExpiresAt)
            throw new ProtocolException("Allowance start cannot be after expiration.");
        return this;
    }
}

public sealed class ProtocolException(string message, Exception? innerException = null)
    : Exception(message, innerException);
