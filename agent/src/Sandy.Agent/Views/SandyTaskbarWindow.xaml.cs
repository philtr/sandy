using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Sandy.Agent.Shell;
using Sandy.Core.Launcher;
using Sandy.Core.Sync;
using Sandy.Core.Time;
using Forms = System.Windows.Forms;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Button = System.Windows.Controls.Button;

namespace Sandy.Agent.Views;

public partial class SandyTaskbarWindow : Window
{
    private const int HeightDips = 48;
    private const int WmDpiChanged = 0x02E0;
    private const int AbnPosChanged = 1;
    private const double RunningButtonMinWidth = 92;
    private const double RunningButtonMaxWidth = 155;
    private const double RunningButtonSpacing = 5;
    private readonly Forms.Screen _screen;
    private readonly Action _home;
    private readonly Action _showEditor;
    private readonly Action _showWindowsDesktop;
    private readonly Action _prepareForSessionExit;
    private readonly string _agentVersion;
    private AppBarRegistration? _appBar;
    private HwndSource? _source;
    private uint _taskbarCreatedMessage;
    private IReadOnlyList<TrackedWindow> _runningWindows = [];
    private string _runningWindowsSignature = string.Empty;
    private string _renderedRunningSignature = string.Empty;
    private int _heightPixels = HeightDips;
    private bool _fullscreenHidden;
    private bool _editingAllowed;

    public SandyTaskbarWindow(
        Forms.Screen screen,
        Action home,
        Action showEditor,
        Action showWindowsDesktop,
        Action prepareForSessionExit,
        string agentVersion)
    {
        InitializeComponent();
        _screen = screen;
        _home = home;
        _showEditor = showEditor;
        _showWindowsDesktop = showWindowsDesktop;
        _prepareForSessionExit = prepareForSessionExit;
        _agentVersion = agentVersion;
        SourceInitialized += OnSourceInitialized;
        ContentRendered += OnContentRendered;
        RunningHost.SizeChanged += RunningHost_SizeChanged;
    }

    public string MonitorName => _screen.DeviceName;
    public bool AppBarRegistered { get; private set; }
    public bool IsReady { get; private set; }
    public event EventHandler? ExplorerTaskbarCreated;
    public event EventHandler? Ready;

    public void SetRunning(IReadOnlyList<TrackedWindow> windows)
    {
        var monitoredWindows = windows.Where(window => window.MonitorName == MonitorName).ToArray();
        var signature = string.Join("|", monitoredWindows.Select(window =>
            $"{window.Identity}:{window.Handle}:{window.Title}:{window.Active}:{window.Minimized}:{window.Maximized}"));
        if (signature == _runningWindowsSignature)
            return;

        _runningWindows = monitoredWindows;
        _runningWindowsSignature = signature;
        RenderRunningWindows();
    }

    private void RenderRunningWindows()
    {
        var availableWidth = RunningHost.ActualWidth;
        if (availableWidth <= 0)
            return;

        var capacity = TaskbarWindowLayout.CalculateCapacity(
            availableWidth, RunningButtonMinWidth + RunningButtonSpacing);
        var renderedSignature = $"{capacity}:{Math.Round(availableWidth, 1)}:{_runningWindowsSignature}";
        if (renderedSignature == _renderedRunningSignature)
            return;
        _renderedRunningSignature = renderedSignature;

        var plan = TaskbarWindowLayout.Plan(_runningWindows, capacity, window => window.Identity);
        var itemCount = plan.Groups.Count + (plan.Overflow.Count > 0 ? 1 : 0);
        var buttonWidth = itemCount == 0
            ? RunningButtonMinWidth
            : Math.Clamp(availableWidth / itemCount - RunningButtonSpacing,
                RunningButtonMinWidth, RunningButtonMaxWidth);

        RunningPanel.Children.Clear();
        foreach (var group in plan.Groups)
            RunningPanel.Children.Add(CreateRunningButton(group, buttonWidth));
        if (plan.Overflow.Count > 0)
            RunningPanel.Children.Add(CreateOverflowButton(plan.Overflow, buttonWidth));
    }

    private Button CreateRunningButton(IReadOnlyList<TrackedWindow> group, double width)
    {
        var representative = group.First();
        var active = group.Any(window => window.Active);
        var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new TextBlock
        {
            Text = group.Count > 1 ? $"{representative.Title} ({group.Count})" : representative.Title,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = Math.Max(50, width - 20),
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
            Width = width,
            Margin = new Thickness(RunningButtonSpacing, 0, 0, 0),
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
            if (group.Count == 1)
            {
                TopLevelWindowTracker.ToggleActivate(representative);
                return;
            }
            OpenWindowMenu(button, group);
        };
        button.ContextMenu = CreateWindowContextMenu(group);
        return button;
    }

    private static ContextMenu CreateWindowContextMenu(IReadOnlyList<TrackedWindow> windows)
    {
        var commands = windows.Count == 1
            ? TaskbarWindowMenu.ForSingle(GetWindowState(windows[0]))
            : TaskbarWindowMenu.ForGroup();
        var menu = new ContextMenu();
        foreach (var command in commands)
        {
            if (command.Kind == TaskbarWindowCommandKind.Close)
                menu.Items.Add(new Separator());
            var item = new MenuItem { Header = command.Label };
            item.Click += (_, _) => ApplyWindowCommand(windows, command.Kind);
            menu.Items.Add(item);
        }
        return menu;
    }

    private static TaskbarWindowState GetWindowState(TrackedWindow window) =>
        window.Minimized
            ? TaskbarWindowState.Minimized
            : window.Maximized ? TaskbarWindowState.Maximized : TaskbarWindowState.Normal;

    private static void ApplyWindowCommand(
        IReadOnlyList<TrackedWindow> windows,
        TaskbarWindowCommandKind command)
    {
        foreach (var window in windows)
        {
            switch (command)
            {
                case TaskbarWindowCommandKind.Minimize:
                    TopLevelWindowTracker.Minimize(window.Handle);
                    break;
                case TaskbarWindowCommandKind.Maximize:
                    TopLevelWindowTracker.Maximize(window.Handle);
                    break;
                case TaskbarWindowCommandKind.Restore:
                    TopLevelWindowTracker.Restore(window.Handle);
                    break;
                case TaskbarWindowCommandKind.Close:
                    TopLevelWindowTracker.Close(window.Handle);
                    break;
            }
        }
    }

    private Button CreateOverflowButton(IReadOnlyList<TrackedWindow> windows, double width)
    {
        var button = new Button
        {
            Style = (Style)FindResource("TaskbarButtonStyle"),
            Height = 36,
            Width = width,
            Margin = new Thickness(RunningButtonSpacing, 0, 0, 0),
            Padding = new Thickness(10, 3, 10, 2),
            Content = $"More ({windows.Count})",
            ToolTip = $"Show {windows.Count} more windows"
        };
        button.Click += (_, _) => OpenWindowMenu(button, windows);
        return button;
    }

    private static void OpenWindowMenu(Button button, IReadOnlyList<TrackedWindow> windows)
    {
        var menu = new ContextMenu();
        foreach (var window in windows)
        {
            var item = new MenuItem { Header = window.Title };
            item.Click += (_, _) => TopLevelWindowTracker.ToggleActivate(window);
            menu.Items.Add(item);
        }
        menu.PlacementTarget = button;
        menu.IsOpen = true;
    }

    private void RunningHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderRunningWindows();
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
        RunningHost.SizeChanged -= RunningHost_SizeChanged;
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
        var editApps = new MenuItem { Header = "Edit apps", IsEnabled = _editingAllowed };
        editApps.Click += (_, _) => _showEditor();
        menu.Items.Add(editApps);
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
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = $"Sandy Agent {_agentVersion}", IsEnabled = false });
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
