using Sandy.Core.Configuration;

namespace Sandy.Core.Tests;

public sealed class AgentOptionsTests
{
    [Fact]
    public void Checks_for_agent_updates_every_fifteen_minutes()
    {
        var options = new AgentOptions
        {
            ServerUri = new Uri("https://sandy.test"),
            AgentVersion = "1.0.0"
        };

        Assert.Equal(TimeSpan.FromMinutes(15), options.UpdateCheckInterval);
    }
}
