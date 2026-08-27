using System.Text.Json;
using Sandy.Core.Events;

namespace Sandy.Core.Tests;

public sealed class AgentDiagnosticReporterTests
{
    [Fact]
    public void Enqueues_structured_diagnostics_without_exception_messages_or_stack_traces()
    {
        var sink = new RecordingEventSink();
        var reporter = new AgentDiagnosticReporter(sink);
        var exception = new InvalidOperationException("Bearer secret-device-token");

        reporter.Error(
            component: "audio",
            code: "cue_playback_failed",
            message: "Could not start the screen-time cue.",
            context: new Dictionary<string, object?>
            {
                ["cue"] = "one-minute.wav",
                ["backend"] = "SoundPlayer"
            },
            exception: exception);

        var diagnostic = Assert.Single(sink.Events);
        Assert.Equal(AgentDiagnosticReporter.EventType, diagnostic.EventType);
        Assert.Equal("error", diagnostic.Metadata["severity"]);
        Assert.Equal("audio", diagnostic.Metadata["component"]);
        Assert.Equal("cue_playback_failed", diagnostic.Metadata["code"]);
        Assert.Equal("Could not start the screen-time cue.", diagnostic.Metadata["message"]);

        var context = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(diagnostic.Metadata["context"]);
        Assert.Equal("one-minute.wav", context["cue"]);
        Assert.Equal("SoundPlayer", context["backend"]);

        var error = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(diagnostic.Metadata["exception"]);
        Assert.Equal(nameof(InvalidOperationException), error["type"]);
        Assert.Equal($"0x{exception.HResult:X8}", error["hresult"]);

        var serialized = JsonSerializer.Serialize(diagnostic.Metadata);
        Assert.DoesNotContain("secret-device-token", serialized);
        Assert.DoesNotContain(nameof(exception.StackTrace), serialized);
    }

    [Fact]
    public void Truncates_user_visible_fields_and_limits_context()
    {
        var sink = new RecordingEventSink();
        var reporter = new AgentDiagnosticReporter(sink);
        var context = Enumerable.Range(1, 20)
            .ToDictionary(index => $"field_{index}", index => (object?)new string('x', 600));

        reporter.Info(
            component: new string('c', 100),
            code: new string('d', 120),
            message: new string('m', 800),
            context: context);

        var metadata = Assert.Single(sink.Events).Metadata;
        Assert.Equal(60, Assert.IsType<string>(metadata["component"]).Length);
        Assert.Equal(80, Assert.IsType<string>(metadata["code"]).Length);
        Assert.Equal(500, Assert.IsType<string>(metadata["message"]).Length);
        var savedContext = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(metadata["context"]);
        Assert.Equal(10, savedContext.Count);
        Assert.All(savedContext.Values, value => Assert.True(Assert.IsType<string>(value).Length <= 200));
    }

    [Fact]
    public void Does_not_serialize_unknown_context_objects()
    {
        var sink = new RecordingEventSink();
        var reporter = new AgentDiagnosticReporter(sink);

        reporter.Warning(
            component: "audio",
            code: "unexpected_context",
            message: "An unexpected context value was supplied.",
            context: new Dictionary<string, object?> { ["value"] = new SecretValue() });

        var metadata = Assert.Single(sink.Events).Metadata;
        var context = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(metadata["context"]);
        Assert.Equal("<SecretValue>", context["value"]);
        Assert.DoesNotContain("secret-device-token", JsonSerializer.Serialize(metadata));
    }

    private sealed class RecordingEventSink : IDeviceEventSink
    {
        public List<(string EventType, IReadOnlyDictionary<string, object?> Metadata)> Events { get; } = [];

        public bool TryEnqueue(string eventType, IReadOnlyDictionary<string, object?>? metadata = null)
        {
            Events.Add((eventType, metadata ?? new Dictionary<string, object?>()));
            return true;
        }
    }

    private sealed class SecretValue
    {
        public override string ToString() => "secret-device-token";
    }
}
