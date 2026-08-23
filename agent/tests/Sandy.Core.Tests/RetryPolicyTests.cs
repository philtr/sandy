using Sandy.Core.Networking;

namespace Sandy.Core.Tests;

public sealed class RetryPolicyTests
{
    [Fact]
    public void Delay_is_jittered_and_capped()
    {
        var policy = new RetryPolicy(TimeSpan.FromSeconds(30), new Random(1234));

        var first = policy.DelayForAttempt(0);
        var late = policy.DelayForAttempt(20);

        Assert.InRange(first.TotalSeconds, 0.75, 1.25);
        Assert.InRange(late.TotalSeconds, 22.5, 30);
    }
}
