namespace Sandy.Core.Time;

public enum TimerNotification
{
    FifteenMinutes,
    FiveMinutes,
    FinalMinute,
    Expired,
    Resumed
}

public sealed class WarningTransitions
{
    private TimerPhase _last = TimerPhase.Unknown;
    private long _version = -1;

    public IReadOnlyList<TimerNotification> Observe(TimerReading reading)
    {
        var notifications = new List<TimerNotification>();
        var prior = _last;

        if (_version >= 0 && reading.StateVersion > _version && prior == TimerPhase.Expired && reading.Phase != TimerPhase.Expired)
            notifications.Add(TimerNotification.Resumed);

        // When a sleep or delayed sync skips several thresholds, show only the most
        // urgent warning instead of stacking multiple dialogs at once.
        if (reading.Phase == TimerPhase.Expired && prior != TimerPhase.Expired)
            notifications.Add(TimerNotification.Expired);
        else if (Crossed(prior, reading.Phase, TimerPhase.FinalMinute))
            notifications.Add(TimerNotification.FinalMinute);
        else if (Crossed(prior, reading.Phase, TimerPhase.FiveMinuteWarning))
            notifications.Add(TimerNotification.FiveMinutes);
        else if (Crossed(prior, reading.Phase, TimerPhase.FifteenMinuteWarning))
            notifications.Add(TimerNotification.FifteenMinutes);

        _last = reading.Phase;
        _version = Math.Max(_version, reading.StateVersion);
        return notifications;
    }

    private static bool Crossed(TimerPhase prior, TimerPhase current, TimerPhase threshold) =>
        prior != TimerPhase.Unknown && prior < threshold && current >= threshold;
}
