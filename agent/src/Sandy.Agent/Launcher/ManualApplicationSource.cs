using System.IO;
using System.Runtime.InteropServices;
using Sandy.Core.Launcher;

namespace Sandy.Agent.Launcher;

public sealed record ManualApplicationChoice(
    string Name,
    LauncherPinKind Kind,
    string Target,
    string? IconPath = null,
    string? MatchIdentity = null);

public static class ManualApplicationSource
{
    public static IReadOnlyList<ManualApplicationChoice> EnumerateStartApplications()
    {
        var choices = new List<ManualApplicationChoice>();
        AddStartFolder(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), choices);
        AddStartFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), choices);
        AddPackagedApps(choices);

        return choices
            .GroupBy(item => $"{item.Kind}:{item.Target}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static LauncherPin FromFile(string path)
    {
        if (!LauncherPin.IsSupportedFile(path))
            throw new InvalidDataException("Choose a .lnk, .url, or .exe file.");
        var fullPath = Path.GetFullPath(path);
        return new LauncherPin(
            Guid.NewGuid(),
            Path.GetFileNameWithoutExtension(fullPath),
            LauncherPin.KindForFile(fullPath),
            fullPath,
            fullPath,
            string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase) ? fullPath : null).Validate();
    }

    private static void AddStartFolder(string root, ICollection<ManualApplicationChoice> choices)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return;
        try
        {
            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                         .Where(LauncherPin.IsSupportedFile))
            {
                choices.Add(new ManualApplicationChoice(
                    Path.GetFileNameWithoutExtension(path), LauncherPin.KindForFile(path), path, path));
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip folders that cannot be read. The picker should still work.
        }
        catch (IOException)
        {
            // A Start Menu folder can change during enumeration.
        }
    }

    private static void AddPackagedApps(ICollection<ManualApplicationChoice> choices)
    {
        object? shell = null;
        object? folder = null;
        object? items = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null)
                return;
            shell = Activator.CreateInstance(shellType);
            if (shell is null)
                return;
            dynamic dynamicShell = shell;
            folder = dynamicShell.NameSpace("shell:AppsFolder");
            if (folder is null)
                return;
            dynamic dynamicFolder = folder;
            items = dynamicFolder.Items();
            dynamic dynamicItems = items;
            for (var index = 0; index < dynamicItems.Count; index++)
            {
                object? item = null;
                try
                {
                    item = dynamicItems.Item(index);
                    dynamic dynamicItem = item;
                    var name = (string?)dynamicItem.Name;
                    var appId = (string?)dynamicItem.ExtendedProperty("System.AppUserModel.ID");
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(appId))
                        choices.Add(new ManualApplicationChoice(
                            name, LauncherPinKind.PackagedApp, appId, MatchIdentity: appId));
                }
                catch (COMException)
                {
                    // Skip shell entries that cannot be read.
                }
                finally
                {
                    Release(item);
                }
            }
        }
        catch (COMException)
        {
            // Packaged-app selection is best effort. File selection remains available.
        }
        finally
        {
            Release(items);
            Release(folder);
            Release(shell);
        }
    }

    private static void Release(object? value)
    {
        if (value is not null && System.Runtime.InteropServices.Marshal.IsComObject(value))
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(value);
    }
}
