using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows.Threading;
using Sandy.Core.Events;
using Sandy.Core.Time;

namespace Sandy.Agent.Enforcement;

internal sealed class TimerCuePlayer : IDisposable
{
    private const float DuckMultiplier = 0.5f;
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CompletionGrace = TimeSpan.FromMilliseconds(500);
    private readonly DispatcherTimer _completionTimer = new();
    private SoundPlayer? _player;
    private AudioSessionDucker? _ducker;
    private CueDefinition? _cue;
    private readonly AgentDiagnosticReporter _diagnostics;

    public TimerCuePlayer(IDeviceEventSink events)
    {
        _diagnostics = new AgentDiagnosticReporter(events);
        _completionTimer.Tick += CompletionTimer_Tick;
    }

    public void Play(TimerNotification notification)
    {
        if (!WarningCueSelector.TrySelect(notification, out var warningCue))
            return;

        Stop();
        _cue = CueDefinition.For(warningCue);
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "Audio", _cue.FileName);
        _diagnostics.Info(
            component: "audio",
            code: "cue_playback_requested",
            message: "A screen-time cue reached its playback threshold.",
            context: CueContext(_cue));
        if (!File.Exists(path))
        {
            Trace.TraceWarning($"Screen-time cue asset is missing: {path}");
            _diagnostics.Error(
                component: "audio",
                code: "cue_asset_missing",
                message: "The bundled screen-time cue file is missing.",
                context: CueContext(_cue));
            _cue = null;
            return;
        }

        try
        {
            _ducker = AudioSessionDucker.DuckOtherSessions(DuckMultiplier);
            if (!_ducker.Succeeded)
            {
                _diagnostics.Warning(
                    component: "audio",
                    code: "session_ducking_failed",
                    message: "The cue will play without reducing other application volumes.",
                    context: CueContext(_cue));
            }
            _player = new SoundPlayer(path)
            {
                LoadTimeout = (int)LoadTimeout.TotalMilliseconds
            };
            _player.Load();
            _player.Play();
            var startedContext = new Dictionary<string, object?>(CueContext(_cue))
            {
                ["ducked_session_count"] = _ducker.DuckedSessionCount
            };
            _diagnostics.Info(
                component: "audio",
                code: "cue_playback_started",
                message: "The native Windows audio backend accepted the screen-time cue.",
                context: startedContext);
            StartCompletionTimer(_cue.Duration + CompletionGrace);
        }
        catch (Exception exception)
        {
            Trace.TraceWarning($"Could not play screen-time cue: {exception.Message}");
            _diagnostics.Error(
                component: "audio",
                code: "cue_playback_failed",
                message: "The native Windows audio backend could not start the screen-time cue.",
                context: _cue is null ? null : CueContext(_cue),
                exception: exception);
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
            try { player.Stop(); }
            catch (Exception exception) { Trace.TraceWarning($"Could not stop screen-time cue: {exception.Message}"); }
            player.Dispose();
        }

        try { _ducker?.Dispose(); }
        catch (Exception exception) { Trace.TraceWarning($"Could not restore system audio: {exception.Message}"); }
        _ducker = null;
    }

    private void CompletionTimer_Tick(object? sender, EventArgs e)
    {
        if (_cue is not null)
        {
            _diagnostics.Info(
                component: "audio",
                code: "cue_playback_completed",
                message: "The screen-time cue playback window completed.",
                context: CueContext(_cue));
        }
        Stop();
    }

    private void StartCompletionTimer(TimeSpan interval)
    {
        _completionTimer.Stop();
        _completionTimer.Interval = interval;
        _completionTimer.Start();
    }

    private static Dictionary<string, object?> CueContext(CueDefinition cue) => new()
    {
        ["cue"] = cue.FileName,
        ["backend"] = nameof(SoundPlayer)
    };

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
