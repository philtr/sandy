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

    [Theory]
    [InlineData(TimerPhase.FifteenMinuteWarning, TimerNotification.FifteenMinutes)]
    [InlineData(TimerPhase.FiveMinuteWarning, TimerNotification.FiveMinutes)]
    [InlineData(TimerPhase.FinalMinute, TimerNotification.FinalMinute)]
    public void New_grant_into_warning_interval_emits_resume_and_matching_warning(
        TimerPhase phase,
        TimerNotification warning)
    {
        var transitions = new WarningTransitions();
        transitions.Observe(Reading(TimerPhase.Expired, 4));

        Assert.Equal(
            [TimerNotification.Resumed, warning],
            transitions.Observe(Reading(phase, 5)));
    }

    [Theory]
    [InlineData(TimerNotification.FifteenMinutes, WarningCue.FifteenMinutes)]
    [InlineData(TimerNotification.FiveMinutes, WarningCue.FiveMinutes)]
    [InlineData(TimerNotification.FinalMinute, WarningCue.OneMinute)]
    public void Selects_the_matching_spoken_cue(TimerNotification notification, WarningCue expectedCue)
    {
        Assert.True(WarningCueSelector.TrySelect(notification, out var cue));
        Assert.Equal(expectedCue, cue);
    }

    [Theory]
    [InlineData(TimerNotification.Expired)]
    [InlineData(TimerNotification.Resumed)]
    public void Does_not_select_a_spoken_cue_for_non_warning_notifications(TimerNotification notification) =>
        Assert.False(WarningCueSelector.TrySelect(notification, out _));

    [Fact]
    public void Selects_Stella_assets_by_default()
    {
        var asset = VoiceCueSelector.Select(WarningCue.FiveMinutes, null);

        Assert.Equal("stella", asset.VoiceTheme);
        Assert.Equal("stella-five-minutes.wav", asset.FileName);
    }

    [Fact]
    public void Selects_the_requested_voice_asset()
    {
        var asset = VoiceCueSelector.Select(WarningCue.OneMinute, "blondie");

        Assert.Equal("blondie", asset.VoiceTheme);
        Assert.Equal("blondie-one-minute.wav", asset.FileName);
    }

    [Fact]
    public void Random_theme_selects_an_available_voice()
    {
        var asset = VoiceCueSelector.Select(WarningCue.FifteenMinutes, "random", () => 1);

        Assert.Equal("blondie", asset.VoiceTheme);
        Assert.Equal("blondie-fifteen-minutes.wav", asset.FileName);
    }

    private static TimerReading Reading(TimerPhase phase, long version) =>
        new(phase, TimeSpan.Zero, version, null, true);
}
