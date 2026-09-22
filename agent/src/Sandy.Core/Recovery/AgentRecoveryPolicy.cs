namespace Sandy.Core.Recovery;

public enum GuardianAction
{
    Wait,
    RestoreTaskbar,
    RestartAgent,
    Stop
}

public static class AgentRecoveryPolicy
{
    private static readonly TimeSpan StaleLeaseThreshold = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StableUptimeThreshold = TimeSpan.FromMinutes(1);
    private const int MaximumRecoveryAttempts = 3;

    public static GuardianAction Evaluate(bool parentRunning, bool leaseExists, TimeSpan leaseAge)
    {
        if (!leaseExists)
            return GuardianAction.Stop;
        if (!parentRunning)
            return GuardianAction.RestartAgent;
        if (leaseAge > StaleLeaseThreshold)
            return GuardianAction.RestoreTaskbar;
        return GuardianAction.Wait;
    }

    public static int? NextRecoveryAttempt(int previousAttempt, TimeSpan uptime)
    {
        if (previousAttempt is < 0 or > MaximumRecoveryAttempts || uptime < TimeSpan.Zero)
            return null;

        var attempt = uptime >= StableUptimeThreshold ? 0 : previousAttempt;
        return attempt < MaximumRecoveryAttempts ? attempt + 1 : null;
    }
}
