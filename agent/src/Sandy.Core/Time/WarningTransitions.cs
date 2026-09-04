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
        var resumed = _version >= 0
            && reading.StateVersion > _version
            && prior == TimerPhase.Expired
            && reading.Phase != TimerPhase.Expired;

        if (resumed)
            notifications.Add(TimerNotification.Resumed);

        // If sleep or delayed sync skips thresholds, show only the most urgent
        // warning. A new allowance starts in Active so short grants warn correctly.
        var warningPrior = resumed ? TimerPhase.Active : prior;
        if (reading.Phase == TimerPhase.Expired && prior != TimerPhase.Expired)
            notifications.Add(TimerNotification.Expired);
        else if (Crossed(warningPrior, reading.Phase, TimerPhase.FinalMinute))
            notifications.Add(TimerNotification.FinalMinute);
        else if (Crossed(warningPrior, reading.Phase, TimerPhase.FiveMinuteWarning))
            notifications.Add(TimerNotification.FiveMinutes);
        else if (Crossed(warningPrior, reading.Phase, TimerPhase.FifteenMinuteWarning))
            notifications.Add(TimerNotification.FifteenMinutes);

        _last = reading.Phase;
        _version = Math.Max(_version, reading.StateVersion);
        return notifications;
    }

    private static bool Crossed(TimerPhase prior, TimerPhase current, TimerPhase threshold) =>
        prior != TimerPhase.Unknown && prior < threshold && current >= threshold;
}
