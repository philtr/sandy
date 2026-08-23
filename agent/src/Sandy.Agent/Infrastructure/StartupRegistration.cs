using Microsoft.Win32;

namespace Sandy.Agent.Infrastructure;

public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void EnsureRegistered()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable))
            return;

        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue("Sandy Agent", $"\"{executable}\" --startup", RegistryValueKind.String);
    }
}
