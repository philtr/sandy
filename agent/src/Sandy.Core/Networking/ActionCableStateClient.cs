using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Sandy.Core.Protocol;

namespace Sandy.Core.Networking;

public sealed class ActionCableStateClient : IRealtimeStateClient
{
    private static readonly string Identifier = JsonSerializer.Serialize(
        new Dictionary<string, string> { ["channel"] = "DeviceChannel" }, JsonDefaults.Options);

    public async Task RunAsync(
        Uri cableUri,
        string token,
        Func<RealtimeStateMessage, CancellationToken, Task> onMessage,
        CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol("actioncable-v1-json");
        socket.Options.SetRequestHeader("Authorization", $"Bearer {token}");
        socket.Options.SetRequestHeader("Origin", HttpOrigin(cableUri));
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        await socket.ConnectAsync(cableUri, cancellationToken);

        var subscribe = JsonSerializer.Serialize(new { command = "subscribe", identifier = Identifier }, JsonDefaults.Options);
        await socket.SendAsync(Encoding.UTF8.GetBytes(subscribe), WebSocketMessageType.Text, true, cancellationToken);

        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var json = await ReceiveMessageAsync(socket, cancellationToken);
            if (json is null)
                return;

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.TryGetProperty("type", out var type))
            {
                if (type.GetString() == "reject_subscription")
                    throw new ProtocolException("Action Cable rejected the device subscription.");
                continue;
            }

            if (!root.TryGetProperty("message", out var message))
                continue;

            if (message.ValueKind == JsonValueKind.Object
                && message.TryGetProperty("type", out var messageType)
                && messageType.GetString() == "device_revoked")
            {
                await onMessage(new RealtimeStateMessage(null, DeviceRevoked: true), cancellationToken);
                return;
            }

            var payload = message;
            if (message.ValueKind == JsonValueKind.Object && message.TryGetProperty("timer_state", out var nested))
                payload = nested;

            var snapshot = payload.Deserialize<TimerSnapshot>(JsonDefaults.Options)?.Validate()
                           ?? throw new ProtocolException("Action Cable delivered an invalid timer state.");
            await onMessage(new RealtimeStateMessage(snapshot), cancellationToken);
        }
    }

    private static string HttpOrigin(Uri cableUri)
    {
        var origin = new UriBuilder(cableUri)
        {
            Scheme = cableUri.Scheme == "wss" ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
            Path = string.Empty,
            Query = string.Empty
        };
        return origin.Uri.GetLeftPart(UriPartial.Authority);
    }

    private static async Task<string?> ReceiveMessageAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var writer = new ArrayBufferWriter<byte>();
        while (true)
        {
            var memory = writer.GetMemory(4096);
            var result = await socket.ReceiveAsync(memory, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;
            if (result.MessageType != WebSocketMessageType.Text)
                throw new ProtocolException("Action Cable sent a non-text message.");
            writer.Advance(result.Count);
            if (result.EndOfMessage)
                return Encoding.UTF8.GetString(writer.WrittenSpan);
        }
    }
}
