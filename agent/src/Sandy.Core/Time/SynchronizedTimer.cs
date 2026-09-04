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
    bool HasAuthoritativeState,
    DateTimeOffset? AllowanceStartedAt = null,
    DateTimeOffset? LauncherEditUnlockedUntil = null,
    DateTimeOffset? EstimatedServerNow = null)
{
    public double? AllowanceProgress
    {
        get
        {
            if (AllowanceStartedAt is null || ExpiresAt is null || EstimatedServerNow is null)
                return null;
            var total = ExpiresAt.Value - AllowanceStartedAt.Value;
            if (total <= TimeSpan.Zero)
                return null;
            return Math.Clamp((ExpiresAt.Value - EstimatedServerNow.Value).TotalSeconds / total.TotalSeconds, 0, 1);
        }
    }

    public bool LauncherEditLeaseActive =>
        LauncherEditUnlockedUntil is not null
        && EstimatedServerNow is not null
        && LauncherEditUnlockedUntil > EstimatedServerNow;
}

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

        var monotonicElapsed = clock.Elapsed(_receivedTimestamp, timestamp);
        var wallElapsed = localNow - _localReceivedAt;
        // A backwards local clock change must not extend screen time or the edit
        // lease. Wall time can advance across sleep when monotonic time pauses.
        var correctedElapsed = monotonicElapsed >= wallElapsed ? monotonicElapsed : wallElapsed;
        var projectedServerNow = _snapshot.ServerTime + correctedElapsed;

        if (_snapshot.ExpiresAt is null || _snapshot.TimerStatus == "expired")
            return new(
                TimerPhase.Expired,
                TimeSpan.Zero,
                _snapshot.StateVersion,
                _snapshot.ExpiresAt,
                true,
                _snapshot.AllowanceStartedAt,
                _snapshot.LauncherEditUnlockedUntil,
                projectedServerNow);

        var monotonicRemaining = TimeSpan.FromSeconds(_snapshot.RemainingSeconds) - monotonicElapsed;

        // Project server time with local wall time so sleep and resume are reflected.
        // Use the smaller value so a backwards clock change cannot grant extra time.
        var wallRemaining = _snapshot.ExpiresAt.Value - projectedServerNow;
        var remaining = monotonicRemaining <= wallRemaining ? monotonicRemaining : wallRemaining;

        if (remaining <= TimeSpan.Zero)
            return new(
                TimerPhase.Expired,
                TimeSpan.Zero,
                _snapshot.StateVersion,
                _snapshot.ExpiresAt,
                true,
                _snapshot.AllowanceStartedAt,
                _snapshot.LauncherEditUnlockedUntil,
                projectedServerNow);

        return new(
            PhaseFor(remaining),
            remaining,
            _snapshot.StateVersion,
            _snapshot.ExpiresAt,
            true,
            _snapshot.AllowanceStartedAt,
            _snapshot.LauncherEditUnlockedUntil,
            projectedServerNow);
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
