using System.IO;
using System.Runtime.InteropServices;
using Sandy.Core.Launcher;

namespace Sandy.Agent.Launcher;

public static class ShortcutIconSource
{
    public static string? Resolve(LauncherPin pin)
    {
        if (pin.Kind != LauncherPinKind.Shortcut
            || !string.Equals(Path.GetExtension(pin.Target), ".lnk", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(pin.Target))
            return null;

        object? shell = null;
        object? shortcut = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
                return null;
            shell = Activator.CreateInstance(shellType);
            if (shell is null)
                return null;

            dynamic dynamicShell = shell;
            shortcut = dynamicShell.CreateShortcut(pin.Target);
            dynamic dynamicShortcut = shortcut;
            var iconPath = IconPath((string?)dynamicShortcut.IconLocation);
            if (iconPath is not null)
                return iconPath;

            var targetPath = (string?)dynamicShortcut.TargetPath;
            return !string.IsNullOrWhiteSpace(targetPath) && File.Exists(targetPath) ? targetPath : null;
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            Release(shortcut);
            Release(shell);
        }
    }

    private static string? IconPath(string? iconLocation)
    {
        if (string.IsNullOrWhiteSpace(iconLocation))
            return null;

        var separator = iconLocation.LastIndexOf(',');
        var path = (separator >= 0 ? iconLocation[..separator] : iconLocation).Trim().Trim('"');
        return File.Exists(path) ? path : null;
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
            Marshal.FinalReleaseComObject(value);
    }
}
