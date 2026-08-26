using System.Windows.Threading;
using Sandy.Agent.Enforcement;
using Sandy.Agent.Launcher;
using Sandy.Agent.Shell;
using Sandy.Agent.Updates;
using Sandy.Agent.Views;
using Sandy.Core.Events;
using Sandy.Core.Sync;
using Sandy.Core.Time;

namespace Sandy.Agent;

public sealed class AgentController : IDisposable
{
    private readonly SynchronizedTimer _timer;
    private readonly TimerSynchronizationService _synchronization;
    private readonly IDeviceEventSink _events;
    private readonly IUpdateService _updates;
    private readonly LauncherDesktopManager _launcher;
    private readonly OverlayManager _overlay = new();
    private readonly WarningTransitions _transitions = new();
    private readonly TimerCuePlayer _cuePlayer = new();
    private readonly DispatcherTimer _timerTick = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private CountdownWindow? _countdownWindow;
    private DateTimeOffset? _expiredSince;
    private bool _applyingUpdate;

    public AgentController(
        SynchronizedTimer timer,
        TimerSynchronizationService synchronization,
        IDeviceEventSink events,
        IUpdateService updates,
        LauncherDesktopManager launcher)
    {
        _timer = timer;
        _synchronization = synchronization;
        _events = events;
        _updates = updates;
        _launcher = launcher;
        _timerTick.Tick += Tick;
        _synchronization.ConnectionStateChanged += ConnectionChanged;
    }

    public void Start()
    {
        _timerTick.Start();
        Tick(this, EventArgs.Empty);
    }

    public async Task RunUpdateChecksAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (await _updates.CheckAndDownloadAsync(cancellationToken))
                    _events.TryEnqueue("update_downloaded");
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                _events.TryEnqueue("update_failed");
            }
            await Task.Delay(TimeSpan.FromHours(6), cancellationToken);
        }
    }

    public void Dispose()
    {
        _timerTick.Stop();
        _synchronization.ConnectionStateChanged -= ConnectionChanged;
        _countdownWindow?.Close();
        _cuePlayer.Dispose();
        _overlay.Dispose();
        _launcher.Dispose();
    }

    private async void Tick(object? sender, EventArgs e)
    {
        var reading = _timer.Read();

        var mustBlock = !reading.HasAuthoritativeState || reading.Phase == TimerPhase.Expired;
        if (mustBlock)
        {
            _countdownWindow?.Close();
            _countdownWindow = null;
            _cuePlayer.Stop();
            _overlay.Show();
            _launcher.SetAvailable(false);
            _synchronization.SetOverlayActive(true);
            _expiredSince ??= DateTimeOffset.UtcNow;
        }
        else
        {
            _overlay.Hide();
            _launcher.SetAvailable(true);
            _synchronization.SetOverlayActive(false);
            _expiredSince = null;
            if (reading.Phase == TimerPhase.FinalMinute)
            {
                var target = TopLevelWindowTracker.ForegroundNoticeTarget();
                if (_countdownWindow is null || _countdownWindow.OwnerHandle != target.OwnerHandle)
                {
                    _countdownWindow?.Close();
                    _countdownWindow = ShowCountdown(target.OwnerHandle, target.Screen);
                }
                _countdownWindow.Update(reading.Remaining);
            }
            else
            {
                _countdownWindow?.Close();
                _countdownWindow = null;
            }
        }

        _launcher.UpdateTimer(reading);

        foreach (var notification in _transitions.Observe(reading))
            HandleNotification(notification);

        if (!_applyingUpdate && _updates.HasPendingUpdate && _expiredSince is not null
            && DateTimeOffset.UtcNow - _expiredSince >= TimeSpan.FromSeconds(60))
        {
            _applyingUpdate = true;
            try
            {
                var snapshot = await _synchronization.RefreshAsync();
                if (snapshot.TimerStatus == "expired" || snapshot.RemainingSeconds == 0)
                {
                    _events.TryEnqueue("update_applying");
                    _updates.ApplyAndRestart();
                }
            }
            catch
            {
                // An update must never be applied when the authoritative state cannot
                // be rechecked; retry on a later tick.
            }
            finally
            {
                _applyingUpdate = false;
            }
        }
    }

    private void HandleNotification(TimerNotification notification)
    {
        switch (notification)
        {
            case TimerNotification.FifteenMinutes:
                _cuePlayer.Play(notification);
                ShowWarning("15 minutes of PC screentime remain");
                _events.TryEnqueue("warning_shown", new Dictionary<string, object?> { ["minutes"] = 15 });
                break;
            case TimerNotification.FiveMinutes:
                _cuePlayer.Play(notification);
                ShowWarning("5 minutes of PC screentime remain");
                _events.TryEnqueue("warning_shown", new Dictionary<string, object?> { ["minutes"] = 5 });
                break;
            case TimerNotification.FinalMinute:
                _cuePlayer.Play(notification);
                _events.TryEnqueue("final_countdown_shown");
                break;
            case TimerNotification.Expired:
                _events.TryEnqueue("overlay_shown");
                break;
            case TimerNotification.Resumed:
                _events.TryEnqueue("overlay_hidden");
                break;
        }
    }

    private static CountdownWindow ShowCountdown(nint ownerHandle, System.Windows.Forms.Screen screen)
    {
        var window = new CountdownWindow(screen, ownerHandle);
        window.Show();
        return window;
    }

    private static void ShowWarning(string message)
    {
        var target = TopLevelWindowTracker.ForegroundNoticeTarget();
        new WarningWindow(message, target.Screen, target.OwnerHandle).Show();
    }

    private void ConnectionChanged(object? sender, ConnectionState state) =>
        System.Windows.Application.Current.Dispatcher.Invoke(() => _launcher.UpdateConnection(state));
}
