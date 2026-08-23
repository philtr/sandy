using Sandy.Core.Protocol;

namespace Sandy.Core.Tests;

public sealed class TimerSnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Accepts_expected_v1_shape() => TestSnapshot.Active(Now).Validate();

    [Fact]
    public void Rejects_unknown_schema()
    {
        var snapshot = TestSnapshot.Active(Now) with { SchemaVersion = 2 };
        Assert.Throws<ProtocolException>(() => snapshot.Validate());
    }

    [Fact]
    public void Rejects_active_snapshot_without_expiration()
    {
        var snapshot = TestSnapshot.Active(Now) with { ExpiresAt = null };
        Assert.Throws<ProtocolException>(() => snapshot.Validate());
    }

    [Fact]
    public void Rejects_unreasonable_heartbeat_interval()
    {
        var snapshot = TestSnapshot.Active(Now) with { HeartbeatIntervalSeconds = 1 };
        Assert.Throws<ProtocolException>(() => snapshot.Validate());
    }
}
