namespace Sandy.Core.Launcher;

public sealed record LauncherApplicationChoice(
    string Name,
    LauncherPinKind Kind,
    string Target,
    string? IconPath = null,
    string? MatchIdentity = null,
    string? Disambiguation = null);

public static class LauncherApplicationChoices
{
    public static IReadOnlyList<LauncherApplicationChoice> DistinctByDisplayName(
        IEnumerable<LauncherApplicationChoice> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);

        var distinct = choices
            .GroupBy(IdentityKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(choice => choice.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var duplicateNames = distinct
            .GroupBy(choice => choice.Name.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);

        return distinct
            .Select(choice => duplicateNames.Contains(choice.Name.Trim())
                ? choice with { Disambiguation = Describe(choice) }
                : choice)
            .ToArray();
    }

    private static string IdentityKey(LauncherApplicationChoice choice) =>
        !string.IsNullOrWhiteSpace(choice.MatchIdentity)
            ? $"identity:{choice.MatchIdentity.Trim()}"
            : $"target:{choice.Kind}:{choice.Target.Trim()}";

    private static string Describe(LauncherApplicationChoice choice) => choice.Kind switch
    {
        LauncherPinKind.PackagedApp => "Microsoft Store app",
        LauncherPinKind.Shortcut => $"Shortcut: {choice.Target}",
        LauncherPinKind.Executable => choice.Target,
        _ => choice.Target
    };
}
