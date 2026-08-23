using System.Text.Json;
using Sandy.Core.Protocol;

namespace Sandy.Core.Persistence;

public sealed record CachedSnapshot(
    TimerSnapshot Snapshot,
    DateTimeOffset CachedAtUtc)
{
    public TimerSnapshot Rehydrate(DateTimeOffset now)
    {
        var elapsed = now > CachedAtUtc ? now - CachedAtUtc : TimeSpan.Zero;
        var remaining = Math.Max(0, Snapshot.RemainingSeconds - (long)Math.Ceiling(elapsed.TotalSeconds));
        var status = remaining > 0 && Snapshot.ExpiresAt is not null ? "active" : "expired";
        return Snapshot with
        {
            ServerTime = Snapshot.ServerTime + elapsed,
            RemainingSeconds = remaining,
            TimerStatus = status
        };
    }
}

public interface ISnapshotStore
{
    Task<CachedSnapshot?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(TimerSnapshot snapshot, CancellationToken cancellationToken = default);
}

public sealed class SnapshotStore(string path, TimeProvider? timeProvider = null) : ISnapshotStore
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<CachedSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var cached = await JsonSerializer.DeserializeAsync<CachedSnapshot>(stream, JsonDefaults.Options, cancellationToken);
            return cached is null ? null : cached with { Snapshot = cached.Snapshot.Validate() };
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or ProtocolException)
        {
            return null; // Invalid cache deliberately fails closed in the caller.
        }
    }

    public async Task SaveAsync(TimerSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        snapshot.Validate();
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Snapshot path must include a directory.");
        Directory.CreateDirectory(directory);

        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             4096, FileOptions.WriteThrough | FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    new CachedSnapshot(snapshot, _timeProvider.GetUtcNow()),
                    JsonDefaults.Options,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
