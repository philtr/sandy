namespace Sandy.Core.Events;

public sealed class AgentDiagnosticReporter(IDeviceEventSink events)
{
    public const string EventType = "agent_diagnostic";
    private const int MaxComponentLength = 60;
    private const int MaxCodeLength = 80;
    private const int MaxMessageLength = 500;
    private const int MaxContextEntries = 10;
    private const int MaxContextStringLength = 200;

    public bool Info(
        string component,
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? context = null) =>
        Report("info", component, code, message, context);

    public bool Warning(
        string component,
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? context = null,
        Exception? exception = null) =>
        Report("warning", component, code, message, context, exception);

    public bool Error(
        string component,
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? context = null,
        Exception? exception = null) =>
        Report("error", component, code, message, context, exception);

    private bool Report(
        string severity,
        string component,
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? context,
        Exception? exception = null)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["severity"] = Truncate(severity, MaxComponentLength),
            ["component"] = Truncate(component, MaxComponentLength),
            ["code"] = Truncate(code, MaxCodeLength),
            ["message"] = Truncate(message, MaxMessageLength)
        };

        if (context is not null && context.Count > 0)
        {
            var safeContext = new Dictionary<string, object?>();
            foreach (var pair in context.Take(MaxContextEntries))
                safeContext[Truncate(pair.Key, MaxCodeLength)] = SafeContextValue(pair.Value);
            metadata["context"] = safeContext;
        }

        if (exception is not null)
        {
            metadata["exception"] = new Dictionary<string, object?>
            {
                ["type"] = Truncate(exception.GetType().Name, MaxContextStringLength),
                ["hresult"] = $"0x{exception.HResult:X8}"
            };
        }

        return events.TryEnqueue(EventType, metadata);
    }

    private static object? SafeContextValue(object? value) => value switch
    {
        null => null,
        string text => Truncate(text, MaxContextStringLength),
        char character => character.ToString(),
        Enum enumeration => Truncate(enumeration.ToString(), MaxContextStringLength),
        bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => value,
        _ => $"<{value.GetType().Name}>"
    };

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];
}
