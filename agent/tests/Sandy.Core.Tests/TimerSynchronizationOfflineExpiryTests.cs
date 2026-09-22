using Sandy.Core.Configuration;
using Sandy.Core.Networking;
using Sandy.Core.Persistence;
using Sandy.Core.Protocol;
using Sandy.Core.Sync;
using Sandy.Core.Time;

namespace Sandy.Core.Tests;

public sealed class TimerSynchronizationOfflineExpiryTests
{
    [Fact]
    public async Task Cached_deadline_expires_while_heartbeat_is_pending_and_realtime_is_disconnected()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new FakeClock(now);
        var timer = new SynchronizedTimer(clock);
        var heartbeat = new BlockingApiClient();
        var service = CreateService(timer, heartbeat, new DisconnectedRealtimeClient(),
            new MemorySnapshotStore(TestSnapshot.Active(now, remainingSeconds: 10), now));

        Assert.True(await service.RestoreCachedStateAsync());
        using var cancellation = new CancellationTokenSource();
        var run = service.RunAsync(cancellation.Token);
        try
        {
            await heartbeat.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(timer.Read().HasAuthoritativeState);
            Assert.True(timer.Read().Remaining > TimeSpan.Zero);
            clock.Advance(TimeSpan.FromSeconds(11));
            Assert.Equal(TimerPhase.Expired, timer.Read().Phase);
        }
        finally
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        }
    }

    [Fact]
    public async Task Cached_deadline_expires_after_heartbeat_failure_while_retrying_offline()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new FakeClock(now);
        var timer = new SynchronizedTimer(clock);
        var heartbeat = new FailThenBlockApiClient();
        var service = CreateService(timer, heartbeat, new DisconnectedRealtimeClient(),
            new MemorySnapshotStore(TestSnapshot.Active(now, remainingSeconds: 10), now),
            maximumRetryDelay: TimeSpan.Zero);

        Assert.True(await service.RestoreCachedStateAsync());
        using var cancellation = new CancellationTokenSource();
        var run = service.RunAsync(cancellation.Token);
        try
        {
            await heartbeat.RetryEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(timer.Read().HasAuthoritativeState);
            Assert.True(timer.Read().Remaining > TimeSpan.Zero);
            clock.Advance(TimeSpan.FromSeconds(11));
            Assert.Equal(TimerPhase.Expired, timer.Read().Phase);
        }
        finally
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        }
    }

    [Fact]
    public async Task New_heartbeat_deadline_is_preserved_after_later_heartbeat_fails()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new FakeClock(now);
        var timer = new SynchronizedTimer(clock);
        var heartbeat = new AcceptThenFailAndBlockApiClient(
            TestSnapshot.Active(now, remainingSeconds: 20, version: 2) with { HeartbeatIntervalSeconds = 5 });
        var service = CreateService(timer, heartbeat, new DisconnectedRealtimeClient(),
            new MemorySnapshotStore(TestSnapshot.Active(now, remainingSeconds: 5), now),
            maximumRetryDelay: TimeSpan.Zero);
        var accepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.SnapshotSynchronized += (_, _) => accepted.TrySetResult();

        Assert.True(await service.RestoreCachedStateAsync());
        using var cancellation = new CancellationTokenSource();
        var run = service.RunAsync(cancellation.Token);
        try
        {
            await accepted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(now.AddSeconds(20), timer.Read().ExpiresAt);
            Assert.True(timer.Read().HasAuthoritativeState);
            Assert.True(timer.Read().Remaining > TimeSpan.Zero);

            await heartbeat.RetryEntered.Task.WaitAsync(TimeSpan.FromSeconds(8));
            clock.Advance(TimeSpan.FromSeconds(6));
            Assert.Equal(TimeSpan.FromSeconds(14), timer.Read().Remaining);
            Assert.Equal(2, timer.Read().StateVersion);
            Assert.Equal(now.AddSeconds(20), timer.Read().ExpiresAt);

            clock.Advance(TimeSpan.FromSeconds(14));
            Assert.Equal(TimerPhase.Expired, timer.Read().Phase);
        }
        finally
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        }
    }

    [Fact]
    public async Task Restarted_agent_rehydrates_expired_cache_without_server_connection()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new FakeClock(now);
        var snapshotTime = now.AddSeconds(-11);
        var timer = new SynchronizedTimer(clock);
        var service = CreateService(timer, new BlockingApiClient(), new DisconnectedRealtimeClient(),
            new MemorySnapshotStore(TestSnapshot.Active(snapshotTime, remainingSeconds: 10), snapshotTime));

        Assert.True(await service.RestoreCachedStateAsync());

        Assert.Equal(TimerPhase.Expired, timer.Read().Phase);
    }

    private static TimerSynchronizationService CreateService(
        SynchronizedTimer timer,
        ISandyApiClient api,
        IRealtimeStateClient realtime,
        ISnapshotStore snapshots,
        TimeSpan? maximumRetryDelay = null) =>
        new(api, realtime, new MemoryCredentialStore(), snapshots, timer, new AgentOptions
        {
            ServerUri = new Uri("https://sandy.test"),
            AgentVersion = "test",
            MaximumRetryDelay = maximumRetryDelay ?? TimeSpan.Zero
        });

    private sealed class MemoryCredentialStore : IDeviceCredentialStore
    {
        private static readonly DeviceCredential Credential = new(1, new Uri("https://sandy.test"), "token", Guid.Empty);
        public Task<DeviceCredential?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<DeviceCredential?>(Credential);
        public Task SaveAsync(DeviceCredential credential, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class MemorySnapshotStore(TimerSnapshot snapshot, DateTimeOffset cachedAt) : ISnapshotStore
    {
        private readonly CachedSnapshot _cached = new(snapshot, cachedAt, Guid.Empty);
        public Task<CachedSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<CachedSnapshot?>(_cached);
        public Task<CachedSnapshot?> LoadAsync(Guid enrollmentGeneration, CancellationToken cancellationToken = default) =>
            Task.FromResult<CachedSnapshot?>(enrollmentGeneration == Guid.Empty ? _cached : null);
        public Task SaveAsync(TimerSnapshot value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(TimerSnapshot value, Guid enrollmentGeneration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class DisconnectedRealtimeClient : IRealtimeStateClient
    {
        public Task RunAsync(Uri cableUri, string token,
            Func<RealtimeStateMessage, CancellationToken, Task> onMessage, CancellationToken cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private sealed class BlockingApiClient : ISandyApiClient
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<EnrollmentResponse> EnrollAsync(Uri serverUri, EnrollmentRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TimerSnapshot> GetStateAsync(Uri serverUri, string token, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public async Task<TimerSnapshot> SendHeartbeatAsync(Uri serverUri, string token, HeartbeatRequest request, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new OperationCanceledException(cancellationToken);
        }
        public Task SendEventsAsync(Uri serverUri, string token, DeviceEventBatch batch, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FailThenBlockApiClient : ISandyApiClient
    {
        private int _calls;
        public TaskCompletionSource RetryEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<EnrollmentResponse> EnrollAsync(Uri serverUri, EnrollmentRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TimerSnapshot> GetStateAsync(Uri serverUri, string token, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public async Task<TimerSnapshot> SendHeartbeatAsync(Uri serverUri, string token, HeartbeatRequest request, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _calls) == 1)
                throw new HttpRequestException("offline");
            RetryEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new OperationCanceledException(cancellationToken);
        }
        public Task SendEventsAsync(Uri serverUri, string token, DeviceEventBatch batch, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class AcceptThenFailAndBlockApiClient(TimerSnapshot snapshot) : ISandyApiClient
    {
        private int _calls;
        public TaskCompletionSource RetryEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<EnrollmentResponse> EnrollAsync(Uri serverUri, EnrollmentRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TimerSnapshot> GetStateAsync(Uri serverUri, string token, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public async Task<TimerSnapshot> SendHeartbeatAsync(Uri serverUri, string token, HeartbeatRequest request, CancellationToken cancellationToken = default)
        {
            switch (Interlocked.Increment(ref _calls))
            {
                case 1:
                    return snapshot;
                case 2:
                    throw new HttpRequestException("offline after accepting the new deadline");
                default:
                    RetryEntered.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    throw new OperationCanceledException(cancellationToken);
            }
        }
        public Task SendEventsAsync(Uri serverUri, string token, DeviceEventBatch batch, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
