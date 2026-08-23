using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Sandy.Agent.Enforcement;

public sealed class KeyboardBlocker : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int WmSyskeydown = 0x0104;
    private const int VkTab = 0x09;
    private const int VkEscape = 0x1B;
    private const int VkF4 = 0x73;
    private const int VkLwin = 0x5B;
    private const int VkRwin = 0x5C;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkShift = 0x10;

    private readonly LowLevelKeyboardProc _callback;
    private nint _hook;

    public KeyboardBlocker()
    {
        _callback = HookCallback;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = module is null ? nint.Zero : GetModuleHandle(module.ModuleName);
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback, moduleHandle, 0);
        if (_hook == nint.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Dispose()
    {
        if (_hook != nint.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = nint.Zero;
        }
        GC.SuppressFinalize(this);
    }

    private nint HookCallback(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && ((int)wParam == WmKeydown || (int)wParam == WmSyskeydown))
        {
            var key = Marshal.ReadInt32(lParam);
            var alt = IsDown(VkMenu);
            var control = IsDown(VkControl);
            var shift = IsDown(VkShift);
            var blocked = key is VkLwin or VkRwin
                          || (key == VkTab && alt)
                          || (key == VkEscape && alt)
                          || (key == VkF4 && alt)
                          || (key == VkEscape && control)
                          || (key == VkEscape && control && shift);

            if (blocked)
                return 1;
        }

        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private delegate nint LowLevelKeyboardProc(int code, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, nint module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string? moduleName);
}
