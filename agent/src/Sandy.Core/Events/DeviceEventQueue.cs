using System.Threading.Channels;
using Sandy.Core.Networking;
using Sandy.Core.Persistence;
using Sandy.Core.Protocol;

namespace Sandy.Core.Events;

public interface IDeviceEventSink
{
    bool TryEnqueue(string eventType, IReadOnlyDictionary<string, object?>? metadata = null);
}

public sealed class DeviceEventQueue(
    ISandyApiClient apiClient,
    IDeviceCredentialStore credentialStore,
    TimeProvider? timeProvider = null) : IDeviceEventSink
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly Channel<DeviceEvent> _events = Channel.CreateBounded<DeviceEvent>(new BoundedChannelOptions(256)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false
    });

    public bool TryEnqueue(string eventType, IReadOnlyDictionary<string, object?>? metadata = null) =>
        _events.Writer.TryWrite(new DeviceEvent(Guid.NewGuid(), eventType, _timeProvider.GetUtcNow(), metadata));

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (await _events.Reader.WaitToReadAsync(cancellationToken))
        {
            var batch = new List<DeviceEvent>(20);
            while (batch.Count < 20 && _events.Reader.TryRead(out var item))
                batch.Add(item);

            var credential = await credentialStore.LoadAsync(cancellationToken);
            if (credential is null)
                continue;

            try
            {
                await apiClient.SendEventsAsync(
                    credential.ServerUri, credential.Token, new DeviceEventBatch(batch), cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Keep the queue deliberately lightweight. Idempotent UUIDs make replay safe;
                // requeue best-effort and let heartbeat diagnostics show prolonged failures.
                foreach (var item in batch)
                    _events.Writer.TryWrite(item);
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
    }
}
