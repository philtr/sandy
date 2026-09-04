using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace Sandy.Agent.Views;

internal static class ForegroundNoticePosition
{
    private const int MarginPixels = 20;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private const uint SwpShowWindow = 0x0040;

    public static void Apply(Window window, Forms.Screen screen)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var scale = GetDpiForWindow(handle) / 96d;
        var width = (int)Math.Ceiling(window.Width * scale);
        var height = (int)Math.Ceiling(window.Height * scale);
        var workArea = screen.WorkingArea;
        // Do not make a topmost notice the foreground window. That can minimize
        // an exclusive-fullscreen game or make it leave fullscreen mode.
        SetWindowPos(
            handle,
            new nint(-1),
            workArea.Right - width - MarginPixels,
            workArea.Top + MarginPixels,
            width,
            height,
            SwpNoActivate | SwpNoOwnerZOrder | SwpShowWindow);
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
}
