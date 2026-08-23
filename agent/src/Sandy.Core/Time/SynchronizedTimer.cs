using Sandy.Core.Protocol;

namespace Sandy.Core.Time;

public enum TimerPhase
{
    Unknown,
    Active,
    FifteenMinuteWarning,
    FiveMinuteWarning,
    FinalMinute,
    Expired
}

public sealed record TimerReading(
    TimerPhase Phase,
    TimeSpan Remaining,
    long StateVersion,
    DateTimeOffset? ExpiresAt,
    bool HasAuthoritativeState);

public sealed class SynchronizedTimer(IMonotonicClock clock)
{
    private readonly object _gate = new();
    private TimerSnapshot? _snapshot;
    private DateTimeOffset _localReceivedAt;
    private long _receivedTimestamp;

    public event EventHandler<TimerReading>? StateChanged;

    public bool Synchronize(TimerSnapshot snapshot)
    {
        snapshot.Validate();
        TimerReading reading;

        lock (_gate)
        {
            if (_snapshot is not null)
            {
                if (snapshot.StateVersion < _snapshot.StateVersion)
                    return false;
                if (snapshot.StateVersion == _snapshot.StateVersion && snapshot.ServerTime < _snapshot.ServerTime)
                    return false;
            }

            _snapshot = snapshot;
            _localReceivedAt = clock.UtcNow;
            _receivedTimestamp = clock.Timestamp;
            reading = ReadCore(_localReceivedAt, _receivedTimestamp);
        }

        StateChanged?.Invoke(this, reading);
        return true;
    }

    public TimerReading Read()
    {
        lock (_gate)
            return ReadCore(clock.UtcNow, clock.Timestamp);
    }

    private TimerReading ReadCore(DateTimeOffset localNow, long timestamp)
    {
        if (_snapshot is null)
            return new(TimerPhase.Unknown, TimeSpan.Zero, 0, null, false);

        if (_snapshot.ExpiresAt is null || _snapshot.TimerStatus == "expired")
            return new(TimerPhase.Expired, TimeSpan.Zero, _snapshot.StateVersion, _snapshot.ExpiresAt, true);

        var monotonicRemaining = TimeSpan.FromSeconds(_snapshot.RemainingSeconds)
            - clock.Elapsed(_receivedTimestamp, timestamp);

        // Project server time using local wall time so suspend/resume is reflected even
        // on platforms whose monotonic source pauses during sleep. Taking the minimum
        // prevents a backwards wall-clock change from granting extra time.
        var projectedServerNow = _snapshot.ServerTime + (localNow - _localReceivedAt);
        var wallRemaining = _snapshot.ExpiresAt.Value - projectedServerNow;
        var remaining = monotonicRemaining <= wallRemaining ? monotonicRemaining : wallRemaining;

        if (remaining <= TimeSpan.Zero)
            return new(TimerPhase.Expired, TimeSpan.Zero, _snapshot.StateVersion, _snapshot.ExpiresAt, true);

        return new(PhaseFor(remaining), remaining, _snapshot.StateVersion, _snapshot.ExpiresAt, true);
    }

    public static TimerPhase PhaseFor(TimeSpan remaining) => remaining switch
    {
        { Ticks: <= 0 } => TimerPhase.Expired,
        { TotalSeconds: <= 60 } => TimerPhase.FinalMinute,
        { TotalMinutes: <= 5 } => TimerPhase.FiveMinuteWarning,
        { TotalMinutes: <= 15 } => TimerPhase.FifteenMinuteWarning,
        _ => TimerPhase.Active
    };
}
