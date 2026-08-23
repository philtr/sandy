using Sandy.Core.Protocol;

namespace Sandy.Core.Networking;

public interface ISandyApiClient
{
    Task<EnrollmentResponse> EnrollAsync(
        Uri serverUri,
        EnrollmentRequest request,
        CancellationToken cancellationToken = default);

    Task<TimerSnapshot> GetStateAsync(
        Uri serverUri,
        string token,
        CancellationToken cancellationToken = default);

    Task<TimerSnapshot> SendHeartbeatAsync(
        Uri serverUri,
        string token,
        HeartbeatRequest request,
        CancellationToken cancellationToken = default);

    Task SendEventsAsync(
        Uri serverUri,
        string token,
        DeviceEventBatch batch,
        CancellationToken cancellationToken = default);
}
