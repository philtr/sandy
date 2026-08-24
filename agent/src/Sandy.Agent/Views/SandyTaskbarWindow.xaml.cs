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
    private const int HeightPixels = 82;
    private const int WmDpiChanged = 0x02E0;
    private const int AbnPosChanged = 1;
    private readonly Forms.Screen _screen;
    private readonly Action _home;
    private readonly Action _prepareForSessionExit;
    private AppBarRegistration? _appBar;
    private HwndSource? _source;
    private uint _taskbarCreatedMessage;
    private string _runningSignature = string.Empty;
    private int _heightPixels = HeightPixels;

    public SandyTaskbarWindow(Forms.Screen screen, Action home, Action prepareForSessionExit)
    {
        InitializeComponent();
        _screen = screen;
        _home = home;
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
        foreach (var pin in pins.Take(LauncherPinStore.MaximumPins))
        {
            var button = new Button
            {
                Width = 44,
                Height = 44,
                Margin = new Thickness(4, 0, 0, 0),
                ToolTip = pin.DisplayName,
                Content = pin.DisplayName.Length > 0 ? pin.DisplayName[..1].ToUpper(CultureInfo.CurrentCulture) : "◆"
            };
            var icon = IconLoader.Load(pin);
            if (icon is not null)
                button.Content = new Image { Source = icon, Width = 28, Height = 28 };
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
            var button = new Button
            {
                Height = 48,
                MinWidth = 105,
                MaxWidth = 180,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(12, 0, 12, 0),
                Content = group.Count() > 1 ? $"{representative.Title} ({group.Count()})" : representative.Title,
                ToolTip = representative.Title,
                BorderBrush = group.Any(window => window.Active)
                    ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                    : (System.Windows.Media.Brush)FindResource("BorderBrush")
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
        TaskbarClockText.Text = DateTime.Now.ToString("h:mm", CultureInfo.CurrentCulture);
    }

    public void SetFullscreenHidden(bool hidden)
    {
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
                    Hide();
            }
        }
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
        _heightPixels = (int)Math.Ceiling(HeightPixels * GetDpiForWindow(handle) / 96d);
        _appBar = new AppBarRegistration(handle, _screen, _heightPixels);
        AppBarRegistered = _appBar.Register();
        if (!AppBarRegistered)
            Hide();
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
            _heightPixels = (int)Math.Ceiling(HeightPixels * GetDpiForWindow(window) / 96d);
            _appBar?.UpdateHeight(_heightPixels);
        }
        return nint.Zero;
    }

    private void Home_Click(object sender, RoutedEventArgs e) => _home();

    private void Volume_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            new VolumeWindow { Owner = this }.Show();
        }
        catch (COMException)
        {
            MessageBox.Show("No audio output is currently available.", "Volume", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Network_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(NetworkInterface.GetIsNetworkAvailable() ? "Network connection available" : "No network connection",
            "Network status", MessageBoxButton.OK, MessageBoxImage.Information);

    private void Account_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = Environment.UserName, IsEnabled = false });
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
