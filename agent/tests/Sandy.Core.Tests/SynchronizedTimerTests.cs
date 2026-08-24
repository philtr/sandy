using Sandy.Core.Time;

namespace Sandy.Core.Tests;

public sealed class SynchronizedTimerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Unknown_state_fails_closed()
    {
        var timer = new SynchronizedTimer(new FakeClock(Now));

        var reading = timer.Read();

        Assert.False(reading.HasAuthoritativeState);
        Assert.Equal(TimerPhase.Unknown, reading.Phase);
        Assert.Equal(TimeSpan.Zero, reading.Remaining);
    }

    [Fact]
    public void Counts_down_with_monotonic_elapsed_time()
    {
        var clock = new FakeClock(Now);
        var timer = new SynchronizedTimer(clock);
        timer.Synchronize(TestSnapshot.Active(Now, 1800));

        clock.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(20), timer.Read().Remaining);
    }

    [Fact]
    public void Reports_whole_allowance_progress_and_edit_lease()
    {
        var clock = new FakeClock(Now);
        var timer = new SynchronizedTimer(clock);
        timer.Synchronize(TestSnapshot.Active(Now, 1800) with
        {
            AllowanceStartedAt = Now.AddMinutes(-30),
            LauncherEditUnlockedUntil = Now.AddMinutes(10)
        });

        var reading = timer.Read();

        Assert.Equal(.5, reading.AllowanceProgress!.Value, 3);
        Assert.True(reading.LauncherEditLeaseActive);
    }

    [Fact]
    public void Wall_clock_progress_handles_sleep_when_monotonic_clock_does_not_advance()
    {
        var clock = new FakeClock(Now);
        var timer = new SynchronizedTimer(clock);
        timer.Synchronize(TestSnapshot.Active(Now, 600));

        clock.AdvanceWallOnly(TimeSpan.FromMinutes(11));

        Assert.Equal(TimerPhase.Expired, timer.Read().Phase);
    }

    [Fact]
    public void Backwards_wall_clock_does_not_add_time()
    {
        var clock = new FakeClock(Now);
        var timer = new SynchronizedTimer(clock);
        timer.Synchronize(TestSnapshot.Active(Now, 600));
        clock.Advance(TimeSpan.FromMinutes(2));
        clock.SetWall(Now.AddHours(-1));

        Assert.Equal(TimeSpan.FromMinutes(8), timer.Read().Remaining);
    }

    [Fact]
    public void Backwards_wall_clock_does_not_extend_launcher_edit_lease()
    {
        var clock = new FakeClock(Now);
        var timer = new SynchronizedTimer(clock);
        timer.Synchronize(TestSnapshot.Active(Now) with
        {
            LauncherEditUnlockedUntil = Now.AddMinutes(30)
        });

        clock.Advance(TimeSpan.FromMinutes(31));
        clock.SetWall(Now.AddHours(-2));

        Assert.False(timer.Read().LauncherEditLeaseActive);
    }

    [Fact]
    public void Rejects_older_version_and_older_same_version_snapshot()
    {
        var clock = new FakeClock(Now);
        var timer = new SynchronizedTimer(clock);
        Assert.True(timer.Synchronize(TestSnapshot.Active(Now.AddMinutes(1), version: 2)));

        Assert.False(timer.Synchronize(TestSnapshot.Active(Now.AddMinutes(2), version: 1)));
        Assert.False(timer.Synchronize(TestSnapshot.Active(Now, version: 2)));
        Assert.Equal(2, timer.Read().StateVersion);
    }

    [Theory]
    [InlineData(901, TimerPhase.Active)]
    [InlineData(900, TimerPhase.FifteenMinuteWarning)]
    [InlineData(300, TimerPhase.FiveMinuteWarning)]
    [InlineData(60, TimerPhase.FinalMinute)]
    [InlineData(0, TimerPhase.Expired)]
    public void Defines_warning_boundaries(int seconds, TimerPhase expected) =>
        Assert.Equal(expected, SynchronizedTimer.PhaseFor(TimeSpan.FromSeconds(seconds)));
}
