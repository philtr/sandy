using Sandy.Core.Recovery;

namespace Sandy.Core.Tests;

public sealed class AgentRecoveryPolicyTests
{
    [Fact]
    public void Healthy_parent_with_fresh_lease_waits()
    {
        Assert.Equal(GuardianAction.Wait, AgentRecoveryPolicy.Evaluate(true, true, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Hung_parent_with_stale_lease_restores_taskbar_without_restarting()
    {
        Assert.Equal(GuardianAction.RestoreTaskbar, AgentRecoveryPolicy.Evaluate(true, true, TimeSpan.FromSeconds(6)));
    }

    [Fact]
    public void Exited_parent_with_stale_lease_restarts_agent()
    {
        Assert.Equal(GuardianAction.RestartAgent, AgentRecoveryPolicy.Evaluate(false, true, TimeSpan.FromSeconds(6)));
    }

    [Fact]
    public void Missing_lease_stops_guardian_even_if_parent_exited()
    {
        Assert.Equal(GuardianAction.Stop, AgentRecoveryPolicy.Evaluate(false, false, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, null)]
    public void Recovery_attempts_are_bounded(int previousAttempt, int? expected)
    {
        Assert.Equal(expected, AgentRecoveryPolicy.NextRecoveryAttempt(previousAttempt, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void Stable_uptime_resets_consecutive_recovery_count()
    {
        Assert.Equal(1, AgentRecoveryPolicy.NextRecoveryAttempt(3, TimeSpan.FromMinutes(1)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Invalid_recovery_counts_are_rejected(int previousAttempt)
    {
        Assert.Null(AgentRecoveryPolicy.NextRecoveryAttempt(previousAttempt, TimeSpan.Zero));
    }
}
