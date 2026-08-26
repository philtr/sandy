namespace Sandy.Core.Time;

public enum WarningCue
{
    FifteenMinutes,
    FiveMinutes,
    OneMinute
}

public static class WarningCueSelector
{
    public static bool TrySelect(TimerNotification notification, out WarningCue cue)
    {
        switch (notification)
        {
            case TimerNotification.FifteenMinutes:
                cue = WarningCue.FifteenMinutes;
                return true;
            case TimerNotification.FiveMinutes:
                cue = WarningCue.FiveMinutes;
                return true;
            case TimerNotification.FinalMinute:
                cue = WarningCue.OneMinute;
                return true;
            default:
                cue = default;
                return false;
        }
    }
}
