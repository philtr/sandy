using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Sandy.Agent.Launcher;
using Sandy.Core.Launcher;
using Sandy.Core.Sync;
using Sandy.Core.Time;
using DrawingRectangle = System.Drawing.Rectangle;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;
using WpfImage = System.Windows.Controls.Image;

namespace Sandy.Agent.Views;

public partial class LauncherWindow : Window
{
    private const uint SwpShowWindow = 0x0040;
    private readonly DrawingRectangle _bounds;
    private readonly Action _showEditor;

    public LauncherWindow(DrawingRectangle bounds, bool primary, Action showEditor)
    {
        InitializeComponent();
        _bounds = bounds;
        _showEditor = showEditor;
        PrimaryContent.Visibility = primary ? Visibility.Visible : Visibility.Collapsed;
        SourceInitialized += PositionOnMonitor;
        SizeChanged += (_, _) => UpdateResponsiveContentSize();
    }

    public void SetPins(IReadOnlyList<LauncherPin> pins)
    {
        AppsGrid.Children.Clear();
        var rows = Math.Max(1, (int)Math.Ceiling((pins.Count + 1) / 4d));
        AppsGrid.Rows = rows;
        AppsGrid.Height = rows * 148;
        foreach (var pin in pins)
            AppsGrid.Children.Add(CreatePinButton(pin));
        AppsGrid.Children.Add(CreateMoreButton());
    }

    public void UpdateTimer(TimerReading reading)
    {
        RemainingText.Text = reading.HasAuthoritativeState
            ? reading.Phase == TimerPhase.Expired ? "Time’s up" : TimeText.Format(reading.Remaining)
            : "Checking…";
        AllowanceProgress.IsIndeterminate = reading.AllowanceProgress is null && reading.Phase != TimerPhase.Expired;
        AllowanceProgress.Value = reading.AllowanceProgress ?? 0;
    }

    public void UpdateConnection(ConnectionState state)
    {
        ConnectionText.Text = state.ToString();
        ConnectionDot.Fill = new SolidColorBrush(state switch
        {
            ConnectionState.Online => MediaColor.FromRgb(47, 220, 128),
            ConnectionState.Offline => MediaColor.FromRgb(255, 138, 128),
            _ => MediaColor.FromRgb(244, 185, 66)
        });
    }

    public void UpdateClock(DateTimeOffset now) =>
        ClockText.Text = now.ToLocalTime().ToString("h:mm tt", CultureInfo.CurrentCulture);

    public void ShowHome()
    {
        ShowBackdrop();
        Activate();
        Focus();
    }

    public void ShowBackdrop()
    {
        if (!IsVisible)
            Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
    }

    private System.Windows.Controls.Button CreatePinButton(LauncherPin pin)
    {
        var panel = new StackPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
        var source = IconLoader.Load(pin);
        if (source is not null)
            panel.Children.Add(new WpfImage { Source = source, Width = 58, Height = 58, Margin = new Thickness(0, 0, 0, 9) });
        else
            panel.Children.Add(new TextBlock
            {
                Text = pin.Kind == LauncherPinKind.Uri ? "↗" : "◆",
                FontSize = 42,
                Foreground = (MediaBrush)FindResource("MutedBrush"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });
        panel.Children.Add(new TextBlock
        {
            Text = pin.DisplayName,
            FontSize = 16,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 170,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        });

        var button = new System.Windows.Controls.Button
        {
            Style = (Style)FindResource("LauncherTileStyle"),
            Content = panel,
            Margin = new Thickness(6)
        };
        button.Click += (_, _) =>
        {
            try
            {
                PinLauncher.Launch(pin);
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or COMException)
            {
                MessageBox.Show(exception.Message, "Could not open app", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
        return button;
    }

    private System.Windows.Controls.Button CreateMoreButton()
    {
        var panel = new StackPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
        panel.Children.Add(new TextBlock
        {
            Text = "•••",
            FontSize = 40,
            Foreground = (MediaBrush)FindResource("MutedBrush"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        });
        panel.Children.Add(new TextBlock { Text = "More", FontSize = 16, HorizontalAlignment = System.Windows.HorizontalAlignment.Center });
        var button = new System.Windows.Controls.Button
        {
            Style = (Style)FindResource("LauncherTileStyle"),
            Content = panel,
            Margin = new Thickness(6)
        };
        button.Click += (_, _) => _showEditor();
        return button;
    }

    private void PositionOnMonitor(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        SetWindowPos(handle, nint.Zero, _bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, SwpShowWindow);
        UpdateResponsiveContentSize();
    }

    private void UpdateResponsiveContentSize()
    {
        if (PrimaryContent.Visibility != Visibility.Visible || ActualWidth <= 0)
            return;
        PrimaryContent.Width = Math.Clamp(ActualWidth * 0.65, 520, 1100);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
}
