using Sandy.Core.Time;

namespace Sandy.Core.Tests;

internal sealed class FakeClock(DateTimeOffset now) : IMonotonicClock
{
    public DateTimeOffset UtcNow { get; private set; } = now;
    public long Timestamp { get; private set; }

    public TimeSpan Elapsed(long startTimestamp, long endTimestamp) =>
        TimeSpan.FromSeconds(endTimestamp - startTimestamp);

    public void Advance(TimeSpan elapsed)
    {
        UtcNow += elapsed;
        Timestamp += (long)elapsed.TotalSeconds;
    }

    public void AdvanceWallOnly(TimeSpan elapsed) => UtcNow += elapsed;
    public void SetWall(DateTimeOffset value) => UtcNow = value;
}
