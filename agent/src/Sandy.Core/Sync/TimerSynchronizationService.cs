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

public enum EnrollmentFailureReason
{
    Revoked,
    Unauthorized
}

public sealed class EnrollmentFailureEventArgs(EnrollmentFailureReason reason) : EventArgs
{
    public EnrollmentFailureReason Reason { get; } = reason;
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
    private TimerSnapshot? _currentSnapshot;
    private int _revocationRaised;

    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<EnrollmentFailureEventArgs>? EnrollmentRejected;
    public event EventHandler<TimerSnapshot>? SnapshotSynchronized;

    public TimerSnapshot? CurrentSnapshot => Volatile.Read(ref _currentSnapshot);

    public void SetOverlayActive(bool active) => _overlayActive = active;

    public async Task<bool> RestoreCachedStateAsync(CancellationToken cancellationToken = default)
    {
        var credential = await credentialStore.LoadAsync(cancellationToken);
        if (credential is null)
            return false;
        var cached = await snapshotStore.LoadAsync(credential.EnrollmentGeneration, cancellationToken);
        if (cached is null)
            return false;

        var snapshot = cached.Rehydrate(DateTimeOffset.UtcNow);
        if (!timer.Synchronize(snapshot))
            return false;
        Volatile.Write(ref _currentSnapshot, snapshot);
        return true;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var credential = await credentialStore.LoadAsync(cancellationToken)
                         ?? throw new InvalidOperationException("The agent is not enrolled.");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            await Task.WhenAll(
                RunHeartbeatLoopAsync(credential, linked, linked.Token),
                RunRealtimeLoopAsync(credential, linked, linked.Token));
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // A confirmed revocation terminates both synchronization transports.
        }
    }

    public async Task<TimerSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var credential = await credentialStore.LoadAsync(cancellationToken)
                         ?? throw new InvalidOperationException("The agent is not enrolled.");
        var snapshot = await apiClient.GetStateAsync(credential.ServerUri, credential.Token, cancellationToken);
        await AcceptAsync(snapshot, cancellationToken);
        return snapshot;
    }

    private async Task RunHeartbeatLoopAsync(
        DeviceCredential credential,
        CancellationTokenSource linked,
        CancellationToken cancellationToken)
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
            catch (SandyApiException exception) when (exception.IsDeviceRevoked)
            {
                RaiseEnrollmentFailure(EnrollmentFailureReason.Revoked);
                linked.Cancel();
                return;
            }
            catch (SandyApiException exception) when (exception.StatusCode == 401 && exception.ErrorCode == "unauthorized")
            {
                RaiseEnrollmentFailure(EnrollmentFailureReason.Unauthorized);
                linked.Cancel();
                return;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                ConnectionStateChanged?.Invoke(this, ConnectionState.Offline);
                await Task.Delay(_retry.DelayForAttempt(attempt++), cancellationToken);
            }
        }
    }

    private async Task RunRealtimeLoopAsync(
        DeviceCredential credential,
        CancellationTokenSource linked,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await realtimeClient.RunAsync(
                    CableUri(credential.ServerUri),
                    credential.Token,
                    async (message, token) =>
                    {
                        if (message.DeviceRevoked)
                        {
                            RaiseEnrollmentFailure(EnrollmentFailureReason.Revoked);
                            linked.Cancel();
                            return;
                        }
                        if (message.TimerSnapshot is not null)
                            await AcceptAsync(message.TimerSnapshot, credential.EnrollmentGeneration, token);
                    },
                    cancellationToken);
                attempt = 0;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_retry.DelayForAttempt(attempt++), cancellationToken);
            }
        }
    }

    private Task AcceptAsync(TimerSnapshot snapshot, CancellationToken cancellationToken)
        => AcceptWithCurrentCredentialAsync(snapshot, cancellationToken);

    private async Task AcceptWithCurrentCredentialAsync(TimerSnapshot snapshot, CancellationToken cancellationToken)
    {
        var credential = await credentialStore.LoadAsync(cancellationToken)
                         ?? throw new InvalidOperationException("The agent is not enrolled.");
        await AcceptAsync(snapshot, credential.EnrollmentGeneration, cancellationToken);
    }

    private async Task AcceptAsync(
        TimerSnapshot snapshot,
        Guid enrollmentGeneration,
        CancellationToken cancellationToken)
    {
        await _snapshotGate.WaitAsync(cancellationToken);
        try
        {
            if (!timer.Synchronize(snapshot))
                return;
            Volatile.Write(ref _currentSnapshot, snapshot);
            await snapshotStore.SaveAsync(snapshot, enrollmentGeneration, cancellationToken);
            SnapshotSynchronized?.Invoke(this, snapshot);
        }
        finally
        {
            _snapshotGate.Release();
        }
    }

    private void RaiseEnrollmentFailure(EnrollmentFailureReason reason)
    {
        if (Interlocked.Exchange(ref _revocationRaised, 1) == 0)
            EnrollmentRejected?.Invoke(this, new EnrollmentFailureEventArgs(reason));
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
