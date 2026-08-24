using System.Text.Json.Serialization;

namespace Sandy.Core.Launcher;

[JsonConverter(typeof(JsonStringEnumConverter<LauncherPinKind>))]
public enum LauncherPinKind
{
    Shortcut,
    Executable,
    Uri,
    PackagedApp
}

public sealed record LauncherPin(
    Guid Id,
    string DisplayName,
    LauncherPinKind Kind,
    string Target,
    string? IconPath = null,
    string? MatchIdentity = null)
{
    public LauncherPin Validate()
    {
        if (Id == Guid.Empty)
            throw new InvalidDataException("A launcher pin must have an ID.");
        if (string.IsNullOrWhiteSpace(DisplayName) || DisplayName.Length > 100)
            throw new InvalidDataException("A launcher pin must have a display name of at most 100 characters.");
        if (string.IsNullOrWhiteSpace(Target) || Target.Length > 2048)
            throw new InvalidDataException("A launcher pin target is invalid.");

        switch (Kind)
        {
            case LauncherPinKind.Shortcut when !IsSupportedShortcut(Target):
                throw new InvalidDataException("Shortcut pins must target a .lnk or .url file.");
            case LauncherPinKind.Executable when !string.Equals(Path.GetExtension(Target), ".exe", StringComparison.OrdinalIgnoreCase):
                throw new InvalidDataException("Executable pins must target an .exe file.");
            case LauncherPinKind.Uri:
                ValidateUri(Target);
                break;
        }

        return this with { DisplayName = DisplayName.Trim(), Target = Target.Trim() };
    }

    public static bool IsSupportedFile(string path) =>
        IsSupportedShortcut(path)
        || string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase);

    public static LauncherPinKind KindForFile(string path) =>
        string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)
            ? LauncherPinKind.Executable
            : LauncherPinKind.Shortcut;

    private static bool IsSupportedShortcut(string path) =>
        string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Path.GetExtension(path), ".url", StringComparison.OrdinalIgnoreCase);

    private static void ValidateUri(string value)
    {
        if (!System.Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is "javascript" or "data" or "file")
            throw new InvalidDataException("Enter a safe absolute URI.");
    }
}

public sealed record LauncherPinDocument(int SchemaVersion, IReadOnlyList<LauncherPin> Pins);
