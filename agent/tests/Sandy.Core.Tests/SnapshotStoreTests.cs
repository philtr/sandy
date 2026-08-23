using Sandy.Core.Persistence;

namespace Sandy.Core.Tests;

public sealed class SnapshotStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Round_trips_valid_snapshot()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "state.json");
            var store = new SnapshotStore(path, new FixedTimeProvider(Now));
            var snapshot = TestSnapshot.Active(Now, 900, 7);

            await store.SaveAsync(snapshot);
            var loaded = await store.LoadAsync();

            Assert.NotNull(loaded);
            Assert.Equal(snapshot, loaded.Snapshot);
            Assert.Equal(Now, loaded.CachedAtUtc);
            Assert.DoesNotContain(".tmp", Directory.EnumerateFiles(directory).Single(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Corrupt_cache_returns_null_and_fails_closed()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "state.json");
            await File.WriteAllTextAsync(path, "not json");

            Assert.Null(await new SnapshotStore(path).LoadAsync());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Rehydrate_deducts_offline_wall_time()
    {
        var cached = new CachedSnapshot(TestSnapshot.Active(Now, 600), Now);

        var snapshot = cached.Rehydrate(Now.AddMinutes(4));

        Assert.Equal(360, snapshot.RemainingSeconds);
        Assert.Equal("active", snapshot.TimerStatus);
        Assert.Equal(Now.AddMinutes(4), snapshot.ServerTime);
    }

    [Fact]
    public void Rehydrate_marks_elapsed_snapshot_expired()
    {
        var cached = new CachedSnapshot(TestSnapshot.Active(Now, 60), Now);

        var snapshot = cached.Rehydrate(Now.AddMinutes(2));

        Assert.Equal(0, snapshot.RemainingSeconds);
        Assert.Equal("expired", snapshot.TimerStatus);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sandy-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
