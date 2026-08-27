namespace Sandy.Core.Time;

public sealed record VoiceCueAsset(string VoiceTheme, string FileName);

public static class VoiceCueSelector
{
    private static readonly string[] AvailableThemes = [ "stella", "blondie" ];

    public static VoiceCueAsset Select(WarningCue cue, string? theme, Func<int>? randomIndex = null)
    {
        var selectedTheme = theme == "random"
            ? AvailableThemes[(randomIndex ?? (() => Random.Shared.Next(AvailableThemes.Length)))()]
            : AvailableThemes.Contains(theme) ? theme! : "stella";

        return new VoiceCueAsset(selectedTheme, FileNameFor(cue, selectedTheme));
    }

    private static string FileNameFor(WarningCue cue, string theme) => (theme, cue) switch
    {
        ("stella", WarningCue.FifteenMinutes) => "stella-fifteen-minutes.wav",
        ("stella", WarningCue.FiveMinutes) => "stella-five-minutes.wav",
        ("stella", WarningCue.OneMinute) => "stella-one-minute.wav",
        (_, WarningCue.FifteenMinutes) => "blondie-fifteen-minutes.wav",
        (_, WarningCue.FiveMinutes) => "blondie-five-minutes.wav",
        (_, WarningCue.OneMinute) => "blondie-one-minute.wav",
        _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, null)
    };
}
