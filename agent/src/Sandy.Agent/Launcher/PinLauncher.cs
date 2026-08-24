using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Sandy.Core.Launcher;
using Sandy.Agent.Shell;

namespace Sandy.Agent.Launcher;

public static class PinLauncher
{
    public static void Launch(LauncherPin pin)
    {
        pin.Validate();
        var match = pin.MatchIdentity
                    ?? (pin.Kind == LauncherPinKind.Executable ? Path.GetFullPath(pin.Target) : null);
        if (match is not null)
        {
            var existing = new TopLevelWindowTracker().Enumerate()
                .FirstOrDefault(window => string.Equals(window.Identity, match, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                TopLevelWindowTracker.ToggleActivate(existing);
                return;
            }
        }
        if (pin.Kind == LauncherPinKind.PackagedApp)
        {
            var manager = (IApplicationActivationManager)new ApplicationActivationManager();
            var result = manager.ActivateApplication(pin.Target, null, ActivateOptions.None, out _);
            Marshal.ThrowExceptionForHR(result);
            return;
        }

        if (pin.Kind is LauncherPinKind.Shortcut or LauncherPinKind.Executable && !File.Exists(pin.Target))
            throw new FileNotFoundException("The pinned application is no longer available.", pin.Target);

        Process.Start(new ProcessStartInfo(pin.Target) { UseShellExecute = true });
    }

    [Flags]
    private enum ActivateOptions
    {
        None = 0
    }

    [ComImport]
    [Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    private class ApplicationActivationManager
    {
    }

    [ComImport]
    [Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string? arguments,
            ActivateOptions options,
            out uint processId);

        [PreserveSig]
        int ActivateForFile(nint appUserModelId, nint itemArray, [MarshalAs(UnmanagedType.LPWStr)] string verb, out uint processId);

        [PreserveSig]
        int ActivateForProtocol(nint appUserModelId, nint itemArray, out uint processId);
    }
}
