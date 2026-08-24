using System.Runtime.InteropServices;

namespace Sandy.Agent.Shell;

public static class NativeTaskbarController
{
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;

    public static void HideAll()
    {
        ForEachTaskbar(handle => ShowWindow(handle, SwHide));
    }

    public static void ShowAll()
    {
        ForEachTaskbar(handle => ShowWindow(handle, SwShowNoActivate));
    }

    private static void ForEachTaskbar(Action<nint> action)
    {
        var primary = FindWindow("Shell_TrayWnd", null);
        if (primary != nint.Zero)
            action(primary);

        var current = nint.Zero;
        while ((current = FindWindowEx(nint.Zero, current, "Shell_SecondaryTrayWnd", null)) != nint.Zero)
            action(current);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindowEx(nint parent, nint childAfter, string? className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);
}
