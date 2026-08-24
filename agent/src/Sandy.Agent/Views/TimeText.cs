namespace Sandy.Agent.Views;

internal static class TimeText
{
    public static string Format(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
            return "0:00";

        var totalHours = (int)remaining.TotalHours;
        return totalHours > 0
            ? $"{totalHours}:{remaining.Minutes:00}:{remaining.Seconds:00}"
            : $"{remaining.Minutes}:{remaining.Seconds:00}";
    }
}
