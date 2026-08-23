using System.Diagnostics;

namespace Sandy.Core.Time;

public interface IMonotonicClock
{
    DateTimeOffset UtcNow { get; }
    long Timestamp { get; }
    TimeSpan Elapsed(long startTimestamp, long endTimestamp);
}

public sealed class SystemMonotonicClock : IMonotonicClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public long Timestamp => Stopwatch.GetTimestamp();

    public TimeSpan Elapsed(long startTimestamp, long endTimestamp) =>
        Stopwatch.GetElapsedTime(startTimestamp, endTimestamp);
}
