using System.IO;

namespace Sandy.Agent.Infrastructure;

public sealed record AgentPaths(string Root, string CredentialFile, string SnapshotFile)
{
    public static AgentPaths ForCurrentUser()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sandy");
        return new(root, Path.Combine(root, "device.dat"), Path.Combine(root, "timer-state.json"));
    }
}
