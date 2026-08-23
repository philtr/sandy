using Sandy.Core.Configuration;
using Sandy.Core.Networking;
using Sandy.Core.Persistence;
using Sandy.Core.Protocol;
using Sandy.Core.Time;

namespace Sandy.Core.Sync;

public enum ConnectionState
{
    Offline,
    Connecting,
    Online
}

public sealed class TimerSynchronizationService(
    ISandyApiClient apiClient,
    IRealtimeStateClient realtimeClient,
    IDeviceCredentialStore credentialStore,
    ISnapshotStore snapshotStore,
    SynchronizedTimer timer,
    AgentOptions options)
{
    private readonly RetryPolicy _retry = new(options.MaximumRetryDelay);
    private readonly SemaphoreSlim _snapshotGate = new(1, 1);
    private volatile bool _overlayActive;

    public event EventHandler<ConnectionState>? ConnectionStateChanged;

    public void SetOverlayActive(bool active) => _overlayActive = active;

    public async Task<bool> RestoreCachedStateAsync(CancellationToken cancellationToken = default)
    {
        var cached = await snapshotStore.LoadAsync(cancellationToken);
        return cached is not null && timer.Synchronize(cached.Rehydrate(DateTimeOffset.UtcNow));
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var credential = await credentialStore.LoadAsync(cancellationToken)
                         ?? throw new InvalidOperationException("The agent is not enrolled.");

        await Task.WhenAll(
            RunHeartbeatLoopAsync(credential, cancellationToken),
            RunRealtimeLoopAsync(credential, cancellationToken));
    }

    public async Task<TimerSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var credential = await credentialStore.LoadAsync(cancellationToken)
                         ?? throw new InvalidOperationException("The agent is not enrolled.");
        var snapshot = await apiClient.GetStateAsync(credential.ServerUri, credential.Token, cancellationToken);
        await AcceptAsync(snapshot, cancellationToken);
        return snapshot;
    }

    private async Task RunHeartbeatLoopAsync(DeviceCredential credential, CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                ConnectionStateChanged?.Invoke(this, ConnectionState.Connecting);
                var snapshot = await apiClient.SendHeartbeatAsync(
                    credential.ServerUri,
                    credential.Token,
                    new HeartbeatRequest(options.AgentVersion, _overlayActive),
                    cancellationToken);
                await AcceptAsync(snapshot, cancellationToken);
                ConnectionStateChanged?.Invoke(this, ConnectionState.Online);
                attempt = 0;
                var interval = TimeSpan.FromSeconds(snapshot.HeartbeatIntervalSeconds);
                await Task.Delay(interval, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                ConnectionStateChanged?.Invoke(this, ConnectionState.Offline);
                await Task.Delay(_retry.DelayForAttempt(attempt++), cancellationToken);
            }
        }
    }

    private async Task RunRealtimeLoopAsync(DeviceCredential credential, CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await realtimeClient.RunAsync(
                    CableUri(credential.ServerUri), credential.Token, AcceptAsync, cancellationToken);
                attempt = 0;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_retry.DelayForAttempt(attempt++), cancellationToken);
            }
        }
    }

    private async Task AcceptAsync(TimerSnapshot snapshot, CancellationToken cancellationToken)
    {
        await _snapshotGate.WaitAsync(cancellationToken);
        try
        {
            if (!timer.Synchronize(snapshot))
                return;
            await snapshotStore.SaveAsync(snapshot, cancellationToken);
        }
        finally
        {
            _snapshotGate.Release();
        }
    }

    private static Uri CableUri(Uri serverUri)
    {
        var builder = new UriBuilder(new Uri(serverUri, "/cable"))
        {
            Scheme = serverUri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws"
        };
        return builder.Uri;
    }
}
