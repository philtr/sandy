namespace Sandy.Core.Configuration;

public sealed record AgentOptions
{
    public required Uri ServerUri { get; init; }
    public required string AgentVersion { get; init; }
    public TimeSpan DefaultHeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan MaximumRetryDelay { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan UpdateCheckInterval { get; init; } = TimeSpan.FromMinutes(15);

    public Uri ApiUri(string relativePath) => new(ServerUri, relativePath);

    public Uri CableUri()
    {
        var builder = new UriBuilder(new Uri(ServerUri, "/cable"))
        {
            Scheme = ServerUri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws"
        };
        return builder.Uri;
    }
}
