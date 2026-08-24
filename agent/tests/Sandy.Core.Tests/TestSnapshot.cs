using Sandy.Core.Protocol;

namespace Sandy.Core.Tests;

internal static class TestSnapshot
{
    public static TimerSnapshot Active(
        DateTimeOffset serverTime,
        long remainingSeconds = 3600,
        long version = 1) => new()
    {
        SchemaVersion = 1,
        StateVersion = version,
        ServerTime = serverTime,
        ExpiresAt = serverTime.AddSeconds(remainingSeconds),
        AllowanceStartedAt = serverTime.AddMinutes(-30),
        RemainingSeconds = remainingSeconds,
        TimerStatus = "active",
        HeartbeatIntervalSeconds = 30
    };

    public static TimerSnapshot Expired(DateTimeOffset serverTime, long version = 1) => new()
    {
        SchemaVersion = 1,
        StateVersion = version,
        ServerTime = serverTime,
        ExpiresAt = serverTime,
        RemainingSeconds = 0,
        TimerStatus = "expired",
        HeartbeatIntervalSeconds = 30
    };
}
