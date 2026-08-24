using System.Runtime.InteropServices;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace Sandy.Agent.Shell;

public sealed class AppBarRegistration(nint handle, Forms.Screen screen, int height) : IDisposable
{
    public const int CallbackMessage = 0x8001;
    private const uint AbmNew = 0;
    private const uint AbmRemove = 1;
    private const uint AbmQueryPos = 2;
    private const uint AbmSetPos = 3;
    private const uint AbeBottom = 3;
    private bool _registered;
    private bool _ownsReservation;
    private int _height = height;

    public bool Register()
    {
        if (_registered)
            return true;
        if (ExistingBottomReservation() > 0)
        {
            _registered = true;
            _ownsReservation = false;
            Position();
            return true;
        }
        var data = CreateData();
        if (SHAppBarMessage(AbmNew, ref data) == 0)
            return false;
        _registered = true;
        _ownsReservation = true;
        Position();
        return true;
    }

    public void Position()
    {
        if (!_registered)
            return;
        var bounds = screen.Bounds;
        if (!_ownsReservation)
        {
            var reservedHeight = ExistingBottomReservation();
            if (reservedHeight <= 0)
                reservedHeight = _height;
            SetWindowPos(handle, nint.Zero, bounds.Left, bounds.Bottom - reservedHeight,
                bounds.Width, reservedHeight, 0x0010 | 0x0040);
            return;
        }
        var data = CreateData();
        data.Edge = AbeBottom;
        data.Rect = new Rect
        {
            Left = bounds.Left,
            Right = bounds.Right,
            Top = bounds.Bottom - _height,
            Bottom = bounds.Bottom
        };
        SHAppBarMessage(AbmQueryPos, ref data);
        data.Rect.Top = data.Rect.Bottom - _height;
        SHAppBarMessage(AbmSetPos, ref data);
        SetWindowPos(handle, nint.Zero, data.Rect.Left, data.Rect.Top,
            data.Rect.Right - data.Rect.Left, data.Rect.Bottom - data.Rect.Top,
            0x0010 | 0x0040);
    }

    public void UpdateHeight(int value)
    {
        _height = value;
        Position();
    }

    public void Dispose()
    {
        if (!_registered)
            return;
        if (_ownsReservation)
        {
            var data = CreateData();
            SHAppBarMessage(AbmRemove, ref data);
        }
        _registered = false;
        _ownsReservation = false;
    }

    private int ExistingBottomReservation()
    {
        var bounds = screen.Bounds;
        var workingArea = screen.WorkingArea;
        return workingArea.Left == bounds.Left && workingArea.Right == bounds.Right
            ? Math.Max(0, bounds.Bottom - workingArea.Bottom)
            : 0;
    }

    private AppBarData CreateData() => new()
    {
        Size = Marshal.SizeOf<AppBarData>(),
        Window = handle,
        CallbackMessage = AppBarRegistration.CallbackMessage
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public int Size;
        public nint Window;
        public uint CallbackMessage;
        public uint Edge;
        public Rect Rect;
        public nint Parameter;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("shell32.dll")]
    private static extern nint SHAppBarMessage(uint message, ref AppBarData data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
}
