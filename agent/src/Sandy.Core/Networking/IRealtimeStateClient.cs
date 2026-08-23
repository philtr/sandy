using Sandy.Core.Protocol;

namespace Sandy.Core.Networking;

public interface IRealtimeStateClient
{
    Task RunAsync(
        Uri cableUri,
        string token,
        Func<TimerSnapshot, CancellationToken, Task> onSnapshot,
        CancellationToken cancellationToken);
}
