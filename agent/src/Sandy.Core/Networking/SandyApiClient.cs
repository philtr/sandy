using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Sandy.Core.Protocol;

namespace Sandy.Core.Networking;

public sealed class SandyApiClient(HttpClient httpClient) : ISandyApiClient
{
    public async Task<EnrollmentResponse> EnrollAsync(
        Uri serverUri,
        EnrollmentRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            new Uri(serverUri, "/api/v1/enrollments"), request, JsonDefaults.Options, cancellationToken);
        var enrollment = await ReadAsync<EnrollmentResponse>(response, cancellationToken);
        return enrollment with { TimerState = enrollment.TimerState.Validate() };
    }

    public Task<TimerSnapshot> GetStateAsync(
        Uri serverUri,
        string token,
        CancellationToken cancellationToken = default) =>
        SendForStateAsync(HttpMethod.Get, new Uri(serverUri, "/api/v1/state"), token, null, cancellationToken);

    public Task<TimerSnapshot> SendHeartbeatAsync(
        Uri serverUri,
        string token,
        HeartbeatRequest request,
        CancellationToken cancellationToken = default) =>
        SendForStateAsync(HttpMethod.Post, new Uri(serverUri, "/api/v1/heartbeats"), token, request, cancellationToken);

    public async Task SendEventsAsync(
        Uri serverUri,
        string token,
        DeviceEventBatch batch,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, new Uri(serverUri, "/api/v1/events"), token);
        request.Content = JsonContent.Create(batch, options: JsonDefaults.Options);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<TimerSnapshot> SendForStateAsync(
        HttpMethod method,
        Uri uri,
        string token,
        object? content,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, uri, token);
        if (content is not null)
            request.Content = JsonContent.Create(content, options: JsonDefaults.Options);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        return (await ReadAsync<TimerSnapshot>(response, cancellationToken)).Validate();
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonDefaults.Options, cancellationToken)
               ?? throw new ProtocolException("Server returned an empty response.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (body.Length > 512)
            body = body[..512];
        throw new SandyApiException((int)response.StatusCode, body);
    }
}

public sealed class SandyApiException(int statusCode, string responseBody)
    : Exception($"Sandy API returned HTTP {statusCode}: {responseBody}")
{
    public int StatusCode { get; } = statusCode;
}
