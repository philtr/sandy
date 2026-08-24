using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DrawingRectangle = System.Drawing.Rectangle;

namespace Sandy.Agent.Views;

public partial class ExpiredOverlayWindow : Window
{
    private static readonly nint HwndTopmost = new(-1);
    private const uint SwpShowWindow = 0x0040;
    private readonly DrawingRectangle _bounds;
    private bool _allowClose;

    public ExpiredOverlayWindow(DrawingRectangle bounds)
    {
        InitializeComponent();
        _bounds = bounds;
        SourceInitialized += PositionOnMonitor;
    }

    public void CloseForResume()
    {
        _allowClose = true;
        Close();
    }

    public void BringToFront()
    {
        var handle = new WindowInteropHelper(this).Handle;
        SetWindowPos(handle, HwndTopmost, _bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, SwpShowWindow);
        Activate();
        Focus();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = !_allowClose;
        base.OnClosing(e);
    }

    private void PositionOnMonitor(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        SetWindowPos(handle, HwndTopmost, _bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, SwpShowWindow);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        nint hWnd, nint hWndInsertAfter, int x, int y, int width, int height, uint flags);
}
