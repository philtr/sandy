using System.Windows.Threading;
using Sandy.Agent.Enforcement;
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
    private readonly StatusWindow _statusWindow;
    private readonly OverlayManager _overlay = new();
    private readonly WarningTransitions _transitions = new();
    private readonly DispatcherTimer _timerTick = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private CountdownWindow? _countdownWindow;
    private DateTimeOffset? _expiredSince;
    private bool _applyingUpdate;

    public AgentController(
        SynchronizedTimer timer,
        TimerSynchronizationService synchronization,
        IDeviceEventSink events,
        IUpdateService updates,
        StatusWindow statusWindow)
    {
        _timer = timer;
        _synchronization = synchronization;
        _events = events;
        _updates = updates;
        _statusWindow = statusWindow;
        _timerTick.Tick += Tick;
        _synchronization.ConnectionStateChanged += ConnectionChanged;
    }

    public void Start()
    {
        _statusWindow.Show();
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
        _overlay.Dispose();
        _statusWindow.ClosePermanently();
    }

    private async void Tick(object? sender, EventArgs e)
    {
        var reading = _timer.Read();

        var mustBlock = !reading.HasAuthoritativeState || reading.Phase == TimerPhase.Expired;
        if (mustBlock)
        {
            _countdownWindow?.Close();
            _countdownWindow = null;
            _overlay.Show();
            _synchronization.SetOverlayActive(true);
            _expiredSince ??= DateTimeOffset.UtcNow;
        }
        else
        {
            _overlay.Hide();
            _synchronization.SetOverlayActive(false);
            _expiredSince = null;
            if (reading.Phase == TimerPhase.FinalMinute)
            {
                _countdownWindow ??= ShowCountdown();
                _countdownWindow.Update(reading.Remaining);
            }
            else
            {
                _countdownWindow?.Close();
                _countdownWindow = null;
            }
        }

        _statusWindow.UpdateTimer(reading);

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
                new WarningWindow("15 minutes of PC screentime remain").Show();
                _events.TryEnqueue("warning_shown", new Dictionary<string, object?> { ["minutes"] = 15 });
                break;
            case TimerNotification.FiveMinutes:
                new WarningWindow("5 minutes of PC screentime remain").Show();
                _events.TryEnqueue("warning_shown", new Dictionary<string, object?> { ["minutes"] = 5 });
                break;
            case TimerNotification.FinalMinute:
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

    private CountdownWindow ShowCountdown()
    {
        var window = new CountdownWindow();
        window.Show();
        return window;
    }

    private void ConnectionChanged(object? sender, ConnectionState state) =>
        _statusWindow.Dispatcher.Invoke(() => _statusWindow.UpdateConnection(state));
}
