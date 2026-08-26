using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Sandy.Core.Time;

namespace Sandy.Agent.Enforcement;

internal sealed class TimerCuePlayer : IDisposable
{
    private const float DuckMultiplier = 0.5f;
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CompletionGrace = TimeSpan.FromMilliseconds(500);
    private readonly DispatcherTimer _completionTimer = new();
    private MediaPlayer? _player;
    private AudioSessionDucker? _ducker;
    private CueDefinition? _cue;

    public TimerCuePlayer() => _completionTimer.Tick += CompletionTimer_Tick;

    public void Play(TimerNotification notification)
    {
        if (!WarningCueSelector.TrySelect(notification, out var warningCue))
            return;

        Stop();
        _cue = CueDefinition.For(warningCue);
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "Audio", _cue.FileName);
        if (!File.Exists(path))
        {
            Trace.TraceWarning($"Screen-time cue asset is missing: {path}");
            _cue = null;
            return;
        }

        try
        {
            _ducker = AudioSessionDucker.DuckOtherSessions(DuckMultiplier);
            _player = new MediaPlayer();
            _player.MediaOpened += Player_MediaOpened;
            _player.MediaEnded += Player_MediaEnded;
            _player.MediaFailed += Player_MediaFailed;
            _player.Open(new System.Uri(path, System.UriKind.Absolute));
            StartCompletionTimer(LoadTimeout);
        }
        catch (Exception exception)
        {
            Trace.TraceWarning($"Could not play screen-time cue: {exception.Message}");
            Stop();
        }
    }

    public void Dispose()
    {
        Stop();
        _completionTimer.Tick -= CompletionTimer_Tick;
    }

    public void Stop()
    {
        _completionTimer.Stop();
        _cue = null;

        if (_player is not null)
        {
            var player = _player;
            _player = null;
            player.MediaOpened -= Player_MediaOpened;
            player.MediaEnded -= Player_MediaEnded;
            player.MediaFailed -= Player_MediaFailed;
            try { player.Close(); }
            catch (Exception exception) { Trace.TraceWarning($"Could not stop screen-time cue: {exception.Message}"); }
        }

        try { _ducker?.Dispose(); }
        catch (Exception exception) { Trace.TraceWarning($"Could not restore system audio: {exception.Message}"); }
        _ducker = null;
    }

    private void Player_MediaOpened(object? sender, EventArgs e)
    {
        var player = _player;
        if (!ReferenceEquals(sender, player) || player is null || _cue is null)
            return;

        player.Play();
        StartCompletionTimer(_cue.Duration + CompletionGrace);
    }

    private void Player_MediaEnded(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _player))
            Stop();
    }

    private void Player_MediaFailed(object? sender, ExceptionEventArgs e)
    {
        Trace.TraceWarning($"Could not play screen-time cue: {e.ErrorException.Message}");
        if (ReferenceEquals(sender, _player))
            Stop();
    }

    private void CompletionTimer_Tick(object? sender, EventArgs e) => Stop();

    private void StartCompletionTimer(TimeSpan interval)
    {
        _completionTimer.Stop();
        _completionTimer.Interval = interval;
        _completionTimer.Start();
    }

    private sealed record CueDefinition(string FileName, TimeSpan Duration)
    {
        public static CueDefinition For(WarningCue cue) => cue switch
        {
            WarningCue.FifteenMinutes => new CueDefinition("fifteen-minutes.wav", TimeSpan.FromSeconds(2.25)),
            WarningCue.FiveMinutes => new CueDefinition("five-minutes.wav", TimeSpan.FromSeconds(2.12)),
            WarningCue.OneMinute => new CueDefinition("one-minute.wav", TimeSpan.FromSeconds(2.13)),
            _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, null)
        };
    }
}
