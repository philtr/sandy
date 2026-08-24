using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Sandy.Core.Sync;
using Sandy.Core.Time;
using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;

namespace Sandy.Agent.Views;

public partial class StatusWindow : Window
{
    private readonly DrawingIcon _trayIconImage;
    private readonly Forms.NotifyIcon _trayIcon;
    private bool _allowClose;

    public StatusWindow()
    {
        InitializeComponent();

        _trayIconImage = LoadApplicationIcon();
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _trayIconImage,
            Text = "Sandy Gaming Timer",
            Visible = true
        };
        _trayIcon.MouseClick += TrayIcon_MouseClick;
    }

    public void UpdateTimer(TimerReading reading)
    {
        RemainingText.Text = reading.HasAuthoritativeState
            ? reading.Phase == TimerPhase.Expired ? "Time’s up" : Format(reading.Remaining)
            : "Checking…";
    }

    public void UpdateConnection(ConnectionState state)
    {
        ConnectionText.Text = state.ToString();
        ConnectionDot.Fill = new SolidColorBrush(state switch
        {
            ConnectionState.Online => System.Windows.Media.Color.FromRgb(88, 214, 141),
            ConnectionState.Offline => System.Windows.Media.Color.FromRgb(255, 138, 128),
            _ => System.Windows.Media.Color.FromRgb(244, 185, 66)
        });
    }

    public static string Format(TimeSpan remaining)
    {
        var total = Math.Max(0, (long)Math.Ceiling(remaining.TotalSeconds));
        return total >= 3600
            ? $"{total / 3600}:{total % 3600 / 60:00}:{total % 60:00}"
            : $"{total / 60}:{total % 60:00}";
    }

    public void ClosePermanently()
    {
        _allowClose = true;
        _trayIcon.Visible = false;
        _trayIcon.MouseClick -= TrayIcon_MouseClick;
        _trayIcon.Dispose();
        _trayIconImage.Dispose();
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    private void TrayIcon_MouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button != Forms.MouseButtons.Left)
            return;

        Dispatcher.Invoke(ShowAndActivate);
    }

    private void ShowAndActivate()
    {
        if (!IsVisible)
            Show();

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
        Focus();
    }

    private static DrawingIcon LoadApplicationIcon()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var icon = DrawingIcon.ExtractAssociatedIcon(processPath);
            if (icon is not null)
                return icon;
        }

        return (DrawingIcon)System.Drawing.SystemIcons.Application.Clone();
    }
}
