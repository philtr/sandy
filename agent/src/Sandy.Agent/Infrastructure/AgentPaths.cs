using System.IO;

namespace Sandy.Agent.Infrastructure;

public sealed record AgentPaths(
    string Root,
    string CredentialFile,
    string SnapshotFile,
    string LauncherPinsFile,
    string LauncherIconCacheDirectory)
{
    public static AgentPaths ForCurrentUser()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sandy");
        return new(
            root,
            Path.Combine(root, "device.dat"),
            Path.Combine(root, "timer-state.json"),
            Path.Combine(root, "launcher-pins.json"),
            Path.Combine(root, "launcher-icons"));
    }
}
