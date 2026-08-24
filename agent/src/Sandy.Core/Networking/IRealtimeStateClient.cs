using Sandy.Core.Protocol;

namespace Sandy.Core.Networking;

public interface IRealtimeStateClient
{
    Task RunAsync(
        Uri cableUri,
        string token,
        Func<RealtimeStateMessage, CancellationToken, Task> onMessage,
        CancellationToken cancellationToken);
}

public sealed record RealtimeStateMessage(TimerSnapshot? TimerSnapshot, bool DeviceRevoked = false);
