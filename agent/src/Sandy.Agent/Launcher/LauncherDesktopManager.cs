using Microsoft.Win32;
using System.Windows.Threading;
using Sandy.Agent.Input;
using Sandy.Agent.Shell;
using Sandy.Agent.Views;
using Sandy.Core.Launcher;
using Sandy.Core.Sync;
using Sandy.Core.Time;
using Forms = System.Windows.Forms;

namespace Sandy.Agent.Launcher;

public sealed class LauncherDesktopManager : IDisposable
{
    private readonly ILauncherPinStore _pinStore;
    private readonly TaskbarGuardianLease _guardian;
    private readonly LauncherIconCache _iconCache;
    private readonly TopLevelWindowTracker _windowTracker = new();
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly List<LauncherWindow> _launchers = [];
    private readonly List<SandyTaskbarWindow> _taskbars = [];
    private IReadOnlyList<LauncherPin> _pins;
    private TimerReading _reading = new(TimerPhase.Unknown, TimeSpan.Zero, 0, null, false);
    private ConnectionState _connection = ConnectionState.Connecting;
    private HomeShortcutManager? _homeShortcuts;
    private EditAppsWindow? _editor;
    private bool _available;
    private bool _availabilityInitialized;
    private bool _nativeDesktopVisible;
    private bool _taskbarRaisePending;
    private string? _fullscreenCandidateMonitor;
    private nint _fullscreenCandidateWindow;
    private DateTimeOffset _fullscreenCandidateSince;
    private string? _fullscreenMonitor;
    private DateTimeOffset? _fullscreenExitSince;
    private nint _fullscreenWindow;

    public LauncherDesktopManager(
        ILauncherPinStore pinStore,
        IReadOnlyList<LauncherPin> pins,
        TaskbarGuardianLease guardian,
        LauncherIconCache iconCache)
    {
        _pinStore = pinStore;
        _pins = pins;
        _guardian = guardian;
        _iconCache = iconCache;
        SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
        BuildWindows();
        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();
    }

    public bool EditingAllowed =>
        _available && _connection == ConnectionState.Online && _reading.LauncherEditLeaseActive;

    public void SetAvailable(bool available)
    {
        if (_availabilityInitialized && _available == available)
            return;
        _availabilityInitialized = true;
        _available = available;
        if (available)
        {
            foreach (var launcher in _launchers)
                if (!launcher.IsVisible) launcher.Show();
            RestoreAllTaskbars();
            try { _homeShortcuts ??= new HomeShortcutManager(ShowHome); }
            catch (System.ComponentModel.Win32Exception) { }
            HideNativeIfHealthy();
        }
        else
        {
            var nativeDesktopWasVisible = _nativeDesktopVisible;
            _nativeDesktopVisible = false;
            _editor?.Close();
            _editor = null;
            _homeShortcuts?.Dispose();
            _homeShortcuts = null;
            foreach (var taskbar in _taskbars)
                taskbar.SetFullscreenHidden(true);
            if (nativeDesktopWasVisible)
                NativeTaskbarController.HideAll();
        }
    }

    public void UpdateTimer(TimerReading reading)
    {
        _reading = reading;
        foreach (var launcher in _launchers)
            launcher.UpdateTimer(reading);
        UpdateTaskbarState();
        EnforceNativeDesktopAuthorization();
    }

    public void UpdateConnection(ConnectionState state)
    {
        _connection = state;
        foreach (var launcher in _launchers)
            launcher.UpdateConnection(state);
        UpdateTaskbarState();
        EnforceNativeDesktopAuthorization();
    }

    public void ShowHome()
    {
        if (!_available)
            return;
        _nativeDesktopVisible = false;
        if (_fullscreenWindow != nint.Zero)
            TopLevelWindowTracker.Minimize(_fullscreenWindow);
        _fullscreenCandidateMonitor = null;
        _fullscreenCandidateWindow = nint.Zero;
        _fullscreenMonitor = null;
        _fullscreenWindow = nint.Zero;
        foreach (var launcher in _launchers.Where(window => window != _launchers.FirstOrDefault()))
            launcher.ShowBackdrop();
        _launchers.FirstOrDefault()?.ShowHome();
        // The full-monitor launcher is activated above ordinary windows. Reposition the
        // AppBars afterwards so the Sandy taskbar is not left behind the launcher.
        RestoreAllTaskbars();
        HideNativeIfHealthy();
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _homeShortcuts?.Dispose();
        _editor?.Close();
        foreach (var taskbar in _taskbars)
        {
            taskbar.ExplorerTaskbarCreated -= Taskbar_ExplorerTaskbarCreated;
            taskbar.Ready -= Taskbar_Ready;
            taskbar.Close();
        }
        foreach (var launcher in _launchers)
            launcher.Close();
        _taskbars.Clear();
        _launchers.Clear();
        NativeTaskbarController.ShowAll();
    }

    private void BuildWindows()
    {
        foreach (var screen in Forms.Screen.AllScreens.OrderByDescending(screen => screen.Primary))
        {
            var launcher = new LauncherWindow(screen.Bounds, screen.Primary, ShowEditor, QueueTaskbarRaise);
            launcher.SetPins(_pins);
            launcher.UpdateTimer(_reading);
            launcher.UpdateConnection(_connection);
            launcher.Show();
            _launchers.Add(launcher);

            var taskbar = new SandyTaskbarWindow(screen, ShowHome, ShowWindowsDesktop, PrepareForSessionExit);
            taskbar.ExplorerTaskbarCreated += Taskbar_ExplorerTaskbarCreated;
            taskbar.Ready += Taskbar_Ready;
            taskbar.SetPins(_pins);
            taskbar.Show();
            _taskbars.Add(taskbar);
        }
    }

    private void RebuildWindows()
    {
        foreach (var taskbar in _taskbars)
        {
            taskbar.ExplorerTaskbarCreated -= Taskbar_ExplorerTaskbarCreated;
            taskbar.Ready -= Taskbar_Ready;
            taskbar.Close();
        }
        foreach (var launcher in _launchers) launcher.Close();
        _taskbars.Clear();
        _launchers.Clear();
        BuildWindows();
        if (_nativeDesktopVisible)
        {
            foreach (var launcher in _launchers) launcher.Hide();
            foreach (var taskbar in _taskbars) taskbar.SetFullscreenHidden(true);
            NativeTaskbarController.ShowAll();
        }
        else if (!_available)
            foreach (var taskbar in _taskbars) taskbar.SetFullscreenHidden(true);
        else
            HideNativeIfHealthy();
    }

    private void ShowEditor()
    {
        if (_editor is { IsVisible: true })
        {
            _editor.Activate();
            return;
        }
        _editor = new EditAppsWindow(
            _pins, _pinStore, () => EditingAllowed, _iconCache.Cache, SetPins)
        {
            Owner = _launchers.FirstOrDefault()
        };
        _editor.Closed += (_, _) => _editor = null;
        _editor.Show();
    }

    private void SetPins(IReadOnlyList<LauncherPin> pins)
    {
        _pins = pins;
        foreach (var launcher in _launchers) launcher.SetPins(pins);
        foreach (var taskbar in _taskbars) taskbar.SetPins(pins);
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        foreach (var launcher in _launchers) launcher.UpdateClock(DateTimeOffset.Now);
        var windows = _windowTracker.Enumerate();
        foreach (var taskbar in _taskbars) taskbar.SetRunning(windows);
        UpdateTaskbarState();
        if (_available && !_nativeDesktopVisible)
        {
            UpdateFullscreen();
            EnsureTaskbarsHealthy();
        }
    }

    private void UpdateFullscreen()
    {
        var now = DateTimeOffset.UtcNow;
        if (TopLevelWindowTracker.IsForegroundFullscreen(out var handle, out var monitor))
        {
            _fullscreenExitSince = null;
            if (_fullscreenMonitor == monitor)
            {
                _fullscreenWindow = handle;
                _fullscreenCandidateMonitor = null;
                _fullscreenCandidateWindow = nint.Zero;
                return;
            }
            if (_fullscreenCandidateMonitor != monitor || _fullscreenCandidateWindow != handle)
            {
                _fullscreenCandidateMonitor = monitor;
                _fullscreenCandidateWindow = handle;
                _fullscreenCandidateSince = now;
                return;
            }
            if (now - _fullscreenCandidateSince >= TimeSpan.FromMilliseconds(450))
            {
                if (_fullscreenMonitor is not null)
                    _taskbars.FirstOrDefault(taskbar => taskbar.MonitorName == _fullscreenMonitor)
                        ?.SetFullscreenHidden(false);
                _fullscreenMonitor = monitor;
                _fullscreenWindow = handle;
                _taskbars.FirstOrDefault(taskbar => taskbar.MonitorName == monitor)?.SetFullscreenHidden(true);
                _fullscreenCandidateMonitor = null;
                _fullscreenCandidateWindow = nint.Zero;
            }
            return;
        }

        _fullscreenCandidateMonitor = null;
        _fullscreenCandidateWindow = nint.Zero;
        if (_fullscreenMonitor is null)
            return;
        _fullscreenExitSince ??= now;
        if (now - _fullscreenExitSince < TimeSpan.FromMilliseconds(300))
            return;
        _taskbars.FirstOrDefault(taskbar => taskbar.MonitorName == _fullscreenMonitor)?.SetFullscreenHidden(false);
        _fullscreenMonitor = null;
        _fullscreenWindow = nint.Zero;
        _fullscreenExitSince = null;
    }

    private void EnsureTaskbarsHealthy()
    {
        var neededRecovery = false;
        foreach (var taskbar in _taskbars.Where(taskbar => taskbar.MonitorName != _fullscreenMonitor))
        {
            if (taskbar.IsVisible && taskbar.AppBarRegistered)
                continue;
            neededRecovery = true;
            taskbar.EnsureAvailable();
        }
        if (neededRecovery)
            HideNativeIfHealthy();
    }

    private void RestoreAllTaskbars()
    {
        foreach (var taskbar in _taskbars)
            taskbar.SetFullscreenHidden(false);
    }

    private void QueueTaskbarRaise()
    {
        if (_taskbarRaisePending || !_available || _nativeDesktopVisible || _fullscreenMonitor is not null)
            return;
        _taskbarRaisePending = true;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _taskbarRaisePending = false;
            if (!_available || _nativeDesktopVisible || _fullscreenMonitor is not null)
                return;
            RestoreAllTaskbars();
            HideNativeIfHealthy();
        }, DispatcherPriority.Background);
    }

    private void ShowWindowsDesktop()
    {
        if (!EditingAllowed)
            return;
        _nativeDesktopVisible = true;
        _fullscreenCandidateMonitor = null;
        _fullscreenCandidateWindow = nint.Zero;
        _fullscreenMonitor = null;
        _fullscreenWindow = nint.Zero;
        _fullscreenExitSince = null;
        _editor?.Close();
        foreach (var launcher in _launchers)
            launcher.Hide();
        foreach (var taskbar in _taskbars)
            taskbar.SetFullscreenHidden(true);
        NativeTaskbarController.ShowAll();
    }

    private void EnforceNativeDesktopAuthorization()
    {
        if (_nativeDesktopVisible && !EditingAllowed)
            ShowHome();
    }

    private void PrepareForSessionExit()
    {
        foreach (var taskbar in _taskbars)
            taskbar.SetFullscreenHidden(true);
        NativeTaskbarController.ShowAll();
    }

    private void HideNativeIfHealthy()
    {
        if (_nativeDesktopVisible)
        {
            NativeTaskbarController.ShowAll();
            return;
        }
        if (_guardian.IsHealthy && _taskbars.Count > 0
            && _taskbars.All(taskbar => taskbar.MonitorName == _fullscreenMonitor
                                        || taskbar.AppBarRegistered && taskbar.IsReady))
            NativeTaskbarController.HideAll();
        else
            NativeTaskbarController.ShowAll();
    }

    private void UpdateTaskbarState()
    {
        foreach (var taskbar in _taskbars)
            taskbar.UpdateState(_reading, _connection, EditingAllowed);
    }

    private void DisplaySettingsChanged(object? sender, EventArgs e) =>
        System.Windows.Application.Current.Dispatcher.Invoke(RebuildWindows);

    private void Taskbar_ExplorerTaskbarCreated(object? sender, EventArgs e)
    {
        if (!_available || _nativeDesktopVisible || sender is not SandyTaskbarWindow taskbar
            || taskbar.MonitorName == _fullscreenMonitor)
            return;
        taskbar.ReRegisterAppBar();
        HideNativeIfHealthy();
    }

    private void Taskbar_Ready(object? sender, EventArgs e)
    {
        if (_available && !_nativeDesktopVisible)
            HideNativeIfHealthy();
    }
}
