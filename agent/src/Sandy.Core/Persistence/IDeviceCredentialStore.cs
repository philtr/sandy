namespace Sandy.Core.Persistence;

public sealed record DeviceCredential(
    long DeviceId,
    Uri ServerUri,
    string Token,
    Guid EnrollmentGeneration = default);

public interface IDeviceCredentialStore
{
    Task<DeviceCredential?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(DeviceCredential credential, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
