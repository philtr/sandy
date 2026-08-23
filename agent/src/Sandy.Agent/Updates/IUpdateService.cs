namespace Sandy.Agent.Updates;

public interface IUpdateService
{
    bool HasPendingUpdate { get; }
    Task<bool> CheckAndDownloadAsync(CancellationToken cancellationToken = default);
    void ApplyAndRestart();
}

public sealed class DisabledUpdateService : IUpdateService
{
    public bool HasPendingUpdate => false;
    public Task<bool> CheckAndDownloadAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    public void ApplyAndRestart() { }
}
