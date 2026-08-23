using Sandy.Core.Time;

namespace Sandy.Core.Tests;

public sealed class WarningTransitionsTests
{
    [Fact]
    public void Emits_each_threshold_once()
    {
        var transitions = new WarningTransitions();
        Assert.Empty(transitions.Observe(Reading(TimerPhase.Active, 1)));
        Assert.Equal([TimerNotification.FifteenMinutes], transitions.Observe(Reading(TimerPhase.FifteenMinuteWarning, 1)));
        Assert.Empty(transitions.Observe(Reading(TimerPhase.FifteenMinuteWarning, 1)));
        Assert.Equal([TimerNotification.FiveMinutes], transitions.Observe(Reading(TimerPhase.FiveMinuteWarning, 1)));
        Assert.Equal([TimerNotification.FinalMinute], transitions.Observe(Reading(TimerPhase.FinalMinute, 1)));
        Assert.Equal([TimerNotification.Expired], transitions.Observe(Reading(TimerPhase.Expired, 1)));
    }

    [Fact]
    public void Skipped_threshold_emits_only_most_urgent_warning()
    {
        var transitions = new WarningTransitions();
        transitions.Observe(Reading(TimerPhase.Active, 1));

        var notifications = transitions.Observe(Reading(TimerPhase.FiveMinuteWarning, 1));

        Assert.Equal([TimerNotification.FiveMinutes], notifications);
    }

    [Fact]
    public void New_grant_while_expired_emits_resume()
    {
        var transitions = new WarningTransitions();
        transitions.Observe(Reading(TimerPhase.Expired, 4));

        Assert.Equal([TimerNotification.Resumed], transitions.Observe(Reading(TimerPhase.Active, 5)));
    }

    private static TimerReading Reading(TimerPhase phase, long version) =>
        new(phase, TimeSpan.Zero, version, null, true);
}
