using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Sandy.Core.Persistence;
using Sandy.Core.Protocol;

namespace Sandy.Agent.Infrastructure;

public sealed class DpapiDeviceCredentialStore(string path) : IDeviceCredentialStore
{
    private static readonly byte[] Entropy = "Sandy.Agent.DeviceCredential.v1"u8.ToArray();

    public async Task<DeviceCredential?> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
            var plainBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            try
            {
                return JsonSerializer.Deserialize<DeviceCredential>(plainBytes, JsonDefaults.Options);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException
                                              or IOException or CryptographicException or JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(DeviceCredential credential, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var plainBytes = JsonSerializer.SerializeToUtf8Bytes(credential, JsonDefaults.Options);
        try
        {
            var protectedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllBytesAsync(temporary, protectedBytes, cancellationToken);
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }
}
