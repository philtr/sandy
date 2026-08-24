using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Sandy.Agent.Shell;

public sealed record TrackedWindow(
    nint Handle,
    string Title,
    string Identity,
    uint ProcessId,
    bool Minimized,
    bool Active,
    string MonitorName);

public sealed class TopLevelWindowTracker
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int ErrorInsufficientBuffer = 122;

    public IReadOnlyList<TrackedWindow> Enumerate()
    {
        var windows = new List<TrackedWindow>();
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle) || handle == GetShellWindow())
                return true;
            if (IsCloaked(handle))
                return true;
            var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
            if ((style & (WsExToolWindow | WsExNoActivate)) != 0)
                return true;
            var className = ReadClassName(handle);
            if (className is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW")
                return true;

            GetWindowThreadProcessId(handle, out var processId);
            if (processId == Environment.ProcessId)
                return true;
            var title = ReadTitle(handle);
            if (!GetWindowRect(handle, out var rect) || rect.Right <= rect.Left || rect.Bottom <= rect.Top)
                return true;

            string? identity;
            try
            {
                using var process = Process.GetProcessById((int)processId);
                if (process.SessionId != Process.GetCurrentProcess().SessionId)
                    return true;
                identity = ResolveIdentity(handle, process);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
                                                   or System.ComponentModel.Win32Exception)
            {
                identity = $"pid:{processId}";
            }

            if (string.IsNullOrWhiteSpace(identity))
                identity = $"pid:{processId}";

            if (string.IsNullOrWhiteSpace(title))
                title = Path.GetFileNameWithoutExtension(identity);
            var screen = System.Windows.Forms.Screen.FromHandle(handle);
            windows.Add(new TrackedWindow(
                handle, title, identity, processId, IsIconic(handle), GetForegroundWindow() == handle, screen.DeviceName));
            return true;
        }, nint.Zero);
        return windows;
    }

    public static void ToggleActivate(TrackedWindow window)
    {
        if (GetForegroundWindow() == window.Handle)
        {
            ShowWindowAsync(window.Handle, 6);
            return;
        }
        if (window.Minimized)
            ShowWindowAsync(window.Handle, 9);
        SetForegroundWindow(window.Handle);
    }

    public static bool IsForegroundFullscreen(out nint handle, out string monitorName)
    {
        handle = GetForegroundWindow();
        monitorName = string.Empty;
        if (handle == nint.Zero || !IsWindowVisible(handle) || IsIconic(handle))
            return false;
        GetWindowThreadProcessId(handle, out var processId);
        if (processId == Environment.ProcessId || !TryGetExtendedFrameBounds(handle, out var rect))
            return false;
        var screen = System.Windows.Forms.Screen.FromHandle(handle);
        monitorName = screen.DeviceName;
        var bounds = screen.Bounds;
        const int tolerance = 3;
        return Math.Abs(rect.Left - bounds.Left) <= tolerance
               && Math.Abs(rect.Top - bounds.Top) <= tolerance
               && Math.Abs(rect.Right - bounds.Right) <= tolerance
               && Math.Abs(rect.Bottom - bounds.Bottom) <= tolerance;
    }

    public static void Minimize(nint handle)
    {
        if (handle != nint.Zero)
            ShowWindowAsync(handle, 6);
    }

    public static System.Windows.Forms.Screen ForegroundScreen() =>
        System.Windows.Forms.Screen.FromHandle(GetForegroundWindow());

    private static string? ResolveIdentity(nint window, Process process)
    {
        if (string.Equals(process.ProcessName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
        {
            string? childIdentity = null;
            EnumChildWindows(window, (child, _) =>
            {
                GetWindowThreadProcessId(child, out var childProcessId);
                if (childProcessId != 0 && childProcessId != (uint)process.Id
                    && TryGetApplicationUserModelId(childProcessId, out childIdentity))
                    return false;
                return true;
            }, nint.Zero);
            if (childIdentity is not null)
                return childIdentity;
        }

        if (TryGetApplicationUserModelId((uint)process.Id, out var appUserModelId))
            return appUserModelId;
        return process.MainModule?.FileName ?? process.ProcessName;
    }

    private static bool TryGetApplicationUserModelId(uint processId, out string? applicationId)
    {
        applicationId = null;
        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == nint.Zero)
            return false;
        try
        {
            uint length = 0;
            if (GetApplicationUserModelId(process, ref length, null) != ErrorInsufficientBuffer || length == 0)
                return false;
            var value = new StringBuilder((int)length);
            if (GetApplicationUserModelId(process, ref length, value) != 0)
                return false;
            applicationId = value.ToString();
            return !string.IsNullOrWhiteSpace(applicationId);
        }
        finally
        {
            CloseHandle(process);
        }
    }

    private static bool IsCloaked(nint handle) =>
        DwmGetWindowAttribute(handle, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    private static bool TryGetExtendedFrameBounds(nint handle, out Rect rect)
    {
        if (DwmGetWindowAttribute(handle, 9, out rect, Marshal.SizeOf<Rect>()) == 0)
            return true;
        return GetWindowRect(handle, out rect);
    }

    private static string ReadTitle(nint handle)
    {
        var length = GetWindowTextLength(handle);
        if (length <= 0)
            return string.Empty;
        var buffer = new System.Text.StringBuilder(length + 1);
        GetWindowText(handle, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string ReadClassName(nint handle)
    {
        var buffer = new System.Text.StringBuilder(256);
        GetClassName(handle, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private delegate bool EnumWindowsProc(nint handle, nint parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);
    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(nint parent, EnumWindowsProc callback, nint parameter);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint handle);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint handle);
    [DllImport("user32.dll")]
    private static extern nint GetShellWindow();
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint handle);
    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(nint handle, int command);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint handle, out Rect rect);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint handle, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint handle, System.Text.StringBuilder text, int maximum);
    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(nint handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint handle, System.Text.StringBuilder className, int maximum);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint handle, int index);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint handle, int attribute, out Rect value, int size);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint handle, int attribute, out int value, int size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint access, bool inheritHandle, uint processId);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(
        nint process, ref uint applicationUserModelIdLength, StringBuilder? applicationUserModelId);
}
