using System.Net;
using System.Text;
using System.Text.Json;
using Sandy.Core.Networking;
using Sandy.Core.Protocol;

namespace Sandy.Core.Tests;

public sealed class SandyApiClientTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Heartbeat_sends_bearer_and_expected_wire_keys()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        using var http = new HttpClient(new StubHandler(async request =>
        {
            captured = request;
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return JsonResponse(TestSnapshot.Active(Now));
        }));
        var client = new SandyApiClient(http);

        await client.SendHeartbeatAsync(
            new Uri("https://sandy.test"), "secret-token", new HeartbeatRequest("1.2.3", true));

        Assert.NotNull(captured);
        Assert.Equal("https://sandy.test/api/v1/heartbeats", captured.RequestUri!.ToString());
        Assert.Equal("Bearer", captured.Headers.Authorization!.Scheme);
        Assert.Equal("secret-token", captured.Headers.Authorization.Parameter);
        Assert.Contains("\"agent_version\":\"1.2.3\"", body, StringComparison.Ordinal);
        Assert.Contains("\"overlay_active\":true", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Enrollment_reads_wrapped_timer_state()
    {
        var responseBody = JsonSerializer.Serialize(new EnrollmentResponse(
            42, "device-token", TestSnapshot.Expired(Now)), JsonDefaults.Options);
        using var http = new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        })));

        var response = await new SandyApiClient(http).EnrollAsync(
            new Uri("https://sandy.test"), new EnrollmentRequest("ABCD", "Family PC", "1.0.0"));

        Assert.Equal(42, response.DeviceId);
        Assert.Equal("device-token", response.DeviceToken);
        Assert.Equal("expired", response.TimerState.TimerStatus);
    }

    [Fact]
    public async Task Events_use_idempotent_wire_identifiers()
    {
        string? body = null;
        using var http = new HttpClient(new StubHandler(async request =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }));
        var eventId = Guid.Parse("f0f070af-92de-48ea-8697-ec9a86f07495");
        var occurredAt = new DateTimeOffset(2026, 8, 23, 18, 5, 0, TimeSpan.Zero);

        await new SandyApiClient(http).SendEventsAsync(
            new Uri("https://sandy.test"), "token",
            new DeviceEventBatch([new DeviceEvent(eventId, "overlay_shown", occurredAt)]));

        Assert.Contains("\"event_id\":\"f0f070af-92de-48ea-8697-ec9a86f07495\"", body, StringComparison.Ordinal);
        Assert.Contains("\"event_type\":\"overlay_shown\"", body, StringComparison.Ordinal);
        Assert.Contains("\"occurred_at\":", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Api_errors_include_status_without_unbounded_body()
    {
        using var http = new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(new string('x', 1000))
        })));

        var exception = await Assert.ThrowsAsync<SandyApiException>(() =>
            new SandyApiClient(http).GetStateAsync(new Uri("https://sandy.test"), "bad"));

        Assert.Equal(401, exception.StatusCode);
        Assert.True(exception.Message.Length < 600);
    }

    [Fact]
    public async Task Revoked_error_is_machine_readable()
    {
        using var http = new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"error\":\"device_revoked\"}", Encoding.UTF8, "application/json")
        })));

        var exception = await Assert.ThrowsAsync<SandyApiException>(() =>
            new SandyApiClient(http).GetStateAsync(new Uri("https://sandy.test"), "revoked"));

        Assert.True(exception.IsDeviceRevoked);
        Assert.Equal("device_revoked", exception.ErrorCode);
    }

    private static HttpResponseMessage JsonResponse<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value, JsonDefaults.Options), Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
    }
}
