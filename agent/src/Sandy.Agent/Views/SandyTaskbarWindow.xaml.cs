using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Sandy.Agent.Launcher;
using Sandy.Agent.Shell;
using Sandy.Core.Launcher;
using Sandy.Core.Sync;
using Sandy.Core.Time;
using Forms = System.Windows.Forms;
using ContextMenu = System.Windows.Controls.ContextMenu;
using Image = System.Windows.Controls.Image;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using Button = System.Windows.Controls.Button;

namespace Sandy.Agent.Views;

public partial class SandyTaskbarWindow : Window
{
    private const int HeightDips = 48;
    private const int WmDpiChanged = 0x02E0;
    private const int AbnPosChanged = 1;
    private readonly Forms.Screen _screen;
    private readonly Action _home;
    private readonly Action _showWindowsDesktop;
    private readonly Action _prepareForSessionExit;
    private AppBarRegistration? _appBar;
    private HwndSource? _source;
    private uint _taskbarCreatedMessage;
    private string _runningSignature = string.Empty;
    private int _heightPixels = HeightDips;
    private bool _fullscreenHidden;
    private bool _editingAllowed;

    public SandyTaskbarWindow(
        Forms.Screen screen,
        Action home,
        Action showWindowsDesktop,
        Action prepareForSessionExit)
    {
        InitializeComponent();
        _screen = screen;
        _home = home;
        _showWindowsDesktop = showWindowsDesktop;
        _prepareForSessionExit = prepareForSessionExit;
        SourceInitialized += OnSourceInitialized;
        ContentRendered += OnContentRendered;
    }

    public string MonitorName => _screen.DeviceName;
    public bool AppBarRegistered { get; private set; }
    public bool IsReady { get; private set; }
    public event EventHandler? ExplorerTaskbarCreated;
    public event EventHandler? Ready;

    public void SetPins(IReadOnlyList<LauncherPin> pins)
    {
        PinnedPanel.Children.Clear();
        const int visiblePinLimit = 6;
        foreach (var pin in pins.Take(visiblePinLimit))
        {
            var button = new Button
            {
                Style = (Style)FindResource("TaskbarButtonStyle"),
                Width = 34,
                Height = 34,
                Margin = new Thickness(3, 0, 0, 0),
                ToolTip = pin.DisplayName,
                Content = pin.DisplayName.Length > 0 ? pin.DisplayName[..1].ToUpper(CultureInfo.CurrentCulture) : "◆"
            };
            var icon = IconLoader.Load(pin);
            if (icon is not null)
                button.Content = new Image { Source = icon, Width = 22, Height = 22 };
            button.Click += (_, _) =>
            {
                try { PinLauncher.Launch(pin); }
                catch (Exception exception) when (exception is IOException or InvalidOperationException
                                                       or System.ComponentModel.Win32Exception or COMException)
                {
                    MessageBox.Show(exception.Message, "Could not open app", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            PinnedPanel.Children.Add(button);
        }
        if (pins.Count > visiblePinLimit)
        {
            var overflow = new Button
            {
                Style = (Style)FindResource("TaskbarButtonStyle"),
                Width = 34,
                Height = 34,
                Margin = new Thickness(3, 0, 0, 0),
                ToolTip = $"Show all {pins.Count} pinned apps",
                Content = new TextBlock
                {
                    Text = "•••",
                    FontSize = 15,
                    Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush")
                }
            };
            overflow.Click += (_, _) => _home();
            PinnedPanel.Children.Add(overflow);
        }
    }

    public void SetRunning(IReadOnlyList<TrackedWindow> windows)
    {
        var groups = windows.Where(window => window.MonitorName == MonitorName)
            .GroupBy(window => window.Identity, StringComparer.OrdinalIgnoreCase).Take(3).ToArray();
        var signature = string.Join("|", groups.Select(group =>
            $"{group.Key}:{string.Join(',', group.Select(window => $"{window.Handle}:{window.Title}:{window.Active}:{window.Minimized}"))}"));
        if (signature == _runningSignature)
            return;
        _runningSignature = signature;
        RunningPanel.Children.Clear();
        foreach (var group in groups)
        {
            var representative = group.First();
            var active = group.Any(window => window.Active);
            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            content.Children.Add(new TextBlock
            {
                Text = group.Count() > 1 ? $"{representative.Title} ({group.Count()})" : representative.Title,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 135,
                FontSize = 12
            });
            content.Children.Add(new Border
            {
                Height = 2,
                Margin = new Thickness(2, 3, 2, 0),
                CornerRadius = new CornerRadius(1),
                Background = active
                    ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(72, 89, 109))
            });
            var button = new Button
            {
                Style = (Style)FindResource("TaskbarButtonStyle"),
                Height = 36,
                MinWidth = 92,
                MaxWidth = 155,
                Margin = new Thickness(5, 0, 0, 0),
                Padding = new Thickness(10, 3, 10, 2),
                Content = content,
                ToolTip = representative.Title,
                BorderBrush = active
                    ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                    : (System.Windows.Media.Brush)FindResource("BorderBrush"),
                Background = active
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 24, 45, 70))
                    : (System.Windows.Media.Brush)FindResource("TaskbarButtonBrush")
            };
            button.Click += (_, _) =>
            {
                if (group.Count() == 1)
                {
                    TopLevelWindowTracker.ToggleActivate(representative);
                    return;
                }
                var menu = new ContextMenu();
                foreach (var window in group)
                {
                    var item = new MenuItem { Header = window.Title };
                    item.Click += (_, _) => TopLevelWindowTracker.ToggleActivate(window);
                    menu.Items.Add(item);
                }
                menu.PlacementTarget = button;
                menu.IsOpen = true;
            };
            RunningPanel.Children.Add(button);
        }
    }

    public void UpdateState(TimerReading reading, ConnectionState connection, bool editingAllowed)
    {
        _editingAllowed = editingAllowed;
        TaskbarRemainingText.Text = reading.HasAuthoritativeState && reading.Phase != TimerPhase.Expired
            ? TimeText.Format(reading.Remaining)
            : "Time’s up";
        EditLeaseText.Text = editingAllowed
            && reading.LauncherEditUnlockedUntil is not null
            && reading.EstimatedServerNow is not null
            ? $"Editing · {TimeText.Format(reading.LauncherEditUnlockedUntil.Value - reading.EstimatedServerNow.Value)}"
            : string.Empty;
        TaskbarConnectionText.Text = connection.ToString();
        TaskbarConnectionDot.Fill = new System.Windows.Media.SolidColorBrush(connection switch
        {
            ConnectionState.Online => System.Windows.Media.Color.FromRgb(47, 220, 128),
            ConnectionState.Offline => System.Windows.Media.Color.FromRgb(255, 138, 128),
            _ => System.Windows.Media.Color.FromRgb(244, 185, 66)
        });
        var networkAvailable = NetworkInterface.GetIsNetworkAvailable();
        var networkBrush = new System.Windows.Media.SolidColorBrush(networkAvailable
            ? System.Windows.Media.Color.FromRgb(220, 229, 242)
            : System.Windows.Media.Color.FromRgb(255, 138, 128));
        NetworkWaves.Stroke = networkBrush;
        NetworkDot.Fill = networkBrush;
        NetworkStatusIcon.Opacity = networkAvailable ? 1 : 0.7;
        NetworkStatusIcon.ToolTip = networkAvailable ? "Network available" : "Network unavailable";
        TaskbarClockText.Text = DateTime.Now.ToString("h:mm", CultureInfo.CurrentCulture);
    }

    public void SetFullscreenHidden(bool hidden)
    {
        _fullscreenHidden = hidden;
        if (hidden)
        {
            _appBar?.Dispose();
            _appBar = null;
            AppBarRegistered = false;
            Hide();
        }
        else
        {
            if (!IsVisible)
                Show();
            if (_appBar is null)
            {
                var handle = new WindowInteropHelper(this).Handle;
                _appBar = new AppBarRegistration(handle, _screen, _heightPixels);
                AppBarRegistered = _appBar.Register();
                if (!AppBarRegistered)
                {
                    _appBar.Dispose();
                    _appBar = null;
                    Hide();
                }
            }
            else
            {
                _appBar.Position();
            }
        }
    }

    public bool EnsureAvailable()
    {
        if (_fullscreenHidden)
            return false;
        if (!IsVisible || !AppBarRegistered)
            SetFullscreenHidden(false);
        return IsVisible && AppBarRegistered;
    }

    public void ReRegisterAppBar()
    {
        _appBar?.Dispose();
        _appBar = null;
        AppBarRegistered = false;
        SetFullscreenHidden(false);
    }

    protected override void OnClosed(EventArgs e)
    {
        _appBar?.Dispose();
        _source?.RemoveHook(WindowMessage);
        ContentRendered -= OnContentRendered;
        base.OnClosed(e);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WindowMessage);
        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
        _heightPixels = (int)Math.Ceiling(HeightDips * GetDpiForWindow(handle) / 96d);
        _appBar = new AppBarRegistration(handle, _screen, _heightPixels);
        AppBarRegistered = _appBar.Register();
        if (!AppBarRegistered)
        {
            _appBar.Dispose();
            _appBar = null;
            Hide();
        }
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        if (IsReady)
            return;
        IsReady = true;
        Ready?.Invoke(this, EventArgs.Empty);
    }

    private nint WindowMessage(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if ((uint)message == _taskbarCreatedMessage)
            ExplorerTaskbarCreated?.Invoke(this, EventArgs.Empty);
        else if (message == AppBarRegistration.CallbackMessage && wParam == AbnPosChanged)
            _appBar?.Position();
        else if (message == WmDpiChanged)
        {
            _heightPixels = (int)Math.Ceiling(HeightDips * GetDpiForWindow(window) / 96d);
            _appBar?.UpdateHeight(_heightPixels);
        }
        return nint.Zero;
    }

    private void Home_Click(object sender, RoutedEventArgs e) => _home();

    private void Account_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = Environment.UserName, IsEnabled = false });
        if (_editingAllowed)
        {
            var windowsDesktop = new MenuItem { Header = "Open Windows desktop" };
            windowsDesktop.Click += (_, _) => _showWindowsDesktop();
            menu.Items.Add(windowsDesktop);
            menu.Items.Add(new Separator());
        }
        var lockItem = new MenuItem { Header = "Lock" };
        lockItem.Click += (_, _) => LockWorkStation();
        var signOut = new MenuItem { Header = "Sign out" };
        signOut.Click += (_, _) => RunSessionAction(() =>
            Process.Start(new ProcessStartInfo("shutdown.exe", "/l") { UseShellExecute = false }));
        menu.Items.Add(lockItem);
        menu.Items.Add(signOut);
        menu.IsOpen = true;
    }

    private void Power_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        AddPowerItem(menu, "Sleep", () => SetSuspendState(false, false, false));
        AddPowerItem(menu, "Restart", () => RunSessionAction(() =>
            Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { UseShellExecute = false })));
        AddPowerItem(menu, "Shut down", () => RunSessionAction(() =>
            Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 0") { UseShellExecute = false })));
        menu.IsOpen = true;
    }

    private static void AddPowerItem(System.Windows.Controls.ContextMenu menu, string title, Action action)
    {
        var item = new MenuItem { Header = title };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }

    private void RunSessionAction(Action action)
    {
        _prepareForSessionExit();
        action();
    }

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();
    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);
}
