using Sandy.Core.Networking;
using Sandy.Core.Persistence;
using Sandy.Core.Protocol;

namespace Sandy.Core.Sync;

public sealed class EnrollmentService(
    ISandyApiClient apiClient,
    IDeviceCredentialStore credentialStore,
    ISnapshotStore snapshotStore)
{
    public async Task<EnrollmentResponse> EnrollAsync(
        Uri serverUri,
        string joinCode,
        string deviceName,
        string agentVersion,
        CancellationToken cancellationToken = default)
    {
        if (serverUri.Scheme is not ("https" or "http"))
            throw new ArgumentException("Server URL must use HTTP or HTTPS.", nameof(serverUri));
        if (string.IsNullOrWhiteSpace(joinCode))
            throw new ArgumentException("Join code is required.", nameof(joinCode));
        if (string.IsNullOrWhiteSpace(deviceName))
            throw new ArgumentException("Device name is required.", nameof(deviceName));

        var response = await apiClient.EnrollAsync(
            serverUri,
            new EnrollmentRequest(joinCode.Trim(), deviceName.Trim(), agentVersion),
            cancellationToken);

        // Save the token and current state before changing the UI. If the process
        // stops between these writes, startup fails closed and fetches state.
        var generation = Guid.NewGuid();
        await credentialStore.SaveAsync(
            new DeviceCredential(response.DeviceId, serverUri, response.DeviceToken, generation), cancellationToken);
        await snapshotStore.SaveAsync(response.TimerState, generation, cancellationToken);
        return response;
    }
}
