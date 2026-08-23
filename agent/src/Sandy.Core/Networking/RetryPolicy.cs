namespace Sandy.Core.Networking;

public sealed class RetryPolicy(TimeSpan maximumDelay, Random? random = null)
{
    private readonly Random _random = random ?? Random.Shared;

    public TimeSpan DelayForAttempt(int attempt)
    {
        var exponent = Math.Min(Math.Max(attempt, 0), 8);
        var jitter = 0.75 + (_random.NextDouble() * 0.5);
        var seconds = Math.Min(maximumDelay.TotalSeconds, Math.Pow(2, exponent) * jitter);
        return TimeSpan.FromSeconds(seconds);
    }
}
