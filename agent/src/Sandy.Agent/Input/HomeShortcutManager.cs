using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Sandy.Core.Input;

namespace Sandy.Agent.Input;

public sealed class HomeShortcutManager : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WmHotKey = 0x0312;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkShift = 0x10;
    private const int HotKeyId = 0x5341;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;

    private readonly Action _home;
    private readonly LowLevelKeyboardProc _callback;
    private readonly WindowsKeySequence _windowsKeySequence = new();
    private readonly HwndSource _messageWindow;
    private nint _hook;

    public HomeShortcutManager(Action home)
    {
        _home = home;
        _callback = HookCallback;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = module is null ? nint.Zero : GetModuleHandle(module.ModuleName);
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback, moduleHandle, 0);
        if (_hook == nint.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        _messageWindow = new HwndSource(new HwndSourceParameters("Sandy.HomeHotKey")
        {
            ParentWindow = (nint)(-3),
            WindowStyle = 0
        });
        _messageWindow.AddHook(WindowMessage);
        RegisterHotKey(_messageWindow.Handle, HotKeyId, ModAlt | ModControl | ModNoRepeat, 0x53);
    }

    public void Dispose()
    {
        if (_hook != nint.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = nint.Zero;
        }
        UnregisterHotKey(_messageWindow.Handle, HotKeyId);
        _messageWindow.RemoveHook(WindowMessage);
        _messageWindow.Dispose();
    }

    private nint HookCallback(int code, nint message, nint data)
    {
        if (code < 0)
            return CallNextHookEx(_hook, code, message, data);
        var key = Marshal.ReadInt32(data);
        var down = (int)message is WmKeyDown or WmSysKeyDown;
        var up = (int)message is WmKeyUp or WmSysKeyUp;
        if (!down && !up)
            return CallNextHookEx(_hook, code, message, data);

        var decision = _windowsKeySequence.Handle(
            key,
            down,
            key is VkLWin or VkRWin,
            IsDown(VkControl) || IsDown(VkMenu) || IsDown(VkShift));
        if (decision.InvokeHome)
            InvokeHome();
        if (decision.Suppress)
            return 1;
        return CallNextHookEx(_hook, code, message, data);
    }

    private nint WindowMessage(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmHotKey && wParam == HotKeyId)
        {
            handled = true;
            InvokeHome();
        }
        return nint.Zero;
    }

    private void InvokeHome() => System.Windows.Application.Current.Dispatcher.BeginInvoke(_home);
    private static bool IsDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    private delegate nint LowLevelKeyboardProc(int code, nint message, nint data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int id, LowLevelKeyboardProc callback, nint module, uint threadId);
    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(nint window, int id);
}
