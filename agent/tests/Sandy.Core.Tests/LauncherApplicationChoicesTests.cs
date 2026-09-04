using Sandy.Core.Launcher;

namespace Sandy.Core.Tests;

public sealed class LauncherApplicationChoicesTests
{
    [Fact]
    public void Keeps_same_named_choices_when_their_identities_differ()
    {
        var startShortcut = new LauncherApplicationChoice(
            "Calculator", LauncherPinKind.Shortcut, @"C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Calculator.lnk",
            MatchIdentity: "file:c:\\tools\\calculator.exe");
        var appsFolderEntry = new LauncherApplicationChoice(
            "calculator", LauncherPinKind.PackagedApp, "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
            MatchIdentity: "app:microsoft.windowscalculator_8wekyb3d8bbwe!app");

        var choices = LauncherApplicationChoices.DistinctByDisplayName([startShortcut, appsFolderEntry]);

        Assert.Equal(2, choices.Count);
        Assert.Equal(startShortcut.Name, choices[0].Name);
        Assert.Equal(appsFolderEntry.Name, choices[1].Name);
        Assert.Equal($"Shortcut: {startShortcut.Target}", choices[0].Disambiguation);
        Assert.Equal("Microsoft Store app", choices[1].Disambiguation);
    }

    [Fact]
    public void Keeps_one_choice_when_entries_have_the_same_identity()
    {
        var startShortcut = new LauncherApplicationChoice(
            "Calculator", LauncherPinKind.Shortcut, @"C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Calculator.lnk",
            MatchIdentity: "app:microsoft.windowscalculator_8wekyb3d8bbwe!app");
        var appsFolderEntry = new LauncherApplicationChoice(
            "Calculator", LauncherPinKind.PackagedApp, "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
            MatchIdentity: "app:microsoft.windowscalculator_8wekyb3d8bbwe!app");

        var choices = LauncherApplicationChoices.DistinctByDisplayName([startShortcut, appsFolderEntry]);

        Assert.Equal([startShortcut], choices);
    }

    [Fact]
    public void Keeps_apps_with_different_display_names()
    {
        var calculator = new LauncherApplicationChoice("Calculator", LauncherPinKind.PackagedApp, "calculator");
        var paint = new LauncherApplicationChoice("Paint", LauncherPinKind.PackagedApp, "paint");

        var choices = LauncherApplicationChoices.DistinctByDisplayName([paint, calculator]);

        Assert.Equal([calculator, paint], choices);
    }
}
