using Velopack;
using Velopack.Sources;

namespace Sandy.Agent.Updates;

public sealed class VelopackUpdateService : IUpdateService
{
    private readonly UpdateManager _manager;

    public VelopackUpdateService(string publicGitHubRepositoryUrl, bool prerelease)
    {
        var source = new GithubSource(publicGitHubRepositoryUrl, accessToken: null, prerelease);
        _manager = new UpdateManager(source);
    }

    public bool HasPendingUpdate => _manager.UpdatePendingRestart is not null;

    public async Task<bool> CheckAndDownloadAsync(CancellationToken cancellationToken = default)
    {
        if (!_manager.IsInstalled)
            return false;
        cancellationToken.ThrowIfCancellationRequested();
        var update = await _manager.CheckForUpdatesAsync();
        if (update is null)
            return false;
        await _manager.DownloadUpdatesAsync(update, cancelToken: cancellationToken);
        return true;
    }

    public void ApplyAndRestart() => _manager.ApplyUpdatesAndRestart(_manager.UpdatePendingRestart);
}
