using System.IO;
using System.Runtime.InteropServices;
using Sandy.Core.Launcher;

namespace Sandy.Agent.Launcher;

public static class StartApplicationIdentity
{
    public static string? ForFile(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase))
            return $"file:{fullPath}";
        if (!string.Equals(Path.GetExtension(fullPath), ".lnk", StringComparison.OrdinalIgnoreCase))
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
            shortcut = dynamicShell.CreateShortcut(fullPath);
            dynamic dynamicShortcut = shortcut;
            var appId = AppsFolderId((string?)dynamicShortcut.Arguments);
            if (appId is not null)
                return $"app:{appId}";

            var targetPath = (string?)dynamicShortcut.TargetPath;
            return !string.IsNullOrWhiteSpace(targetPath) && File.Exists(targetPath)
                ? $"file:{Path.GetFullPath(targetPath)}"
                : null;
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

    private static string? AppsFolderId(string? arguments)
    {
        const string prefix = "shell:AppsFolder\\";
        if (string.IsNullOrWhiteSpace(arguments))
            return null;
        var index = arguments.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return null;
        var appId = arguments[(index + prefix.Length)..].Trim().Trim('"');
        return string.IsNullOrWhiteSpace(appId) ? null : appId;
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
            Marshal.FinalReleaseComObject(value);
    }
}
