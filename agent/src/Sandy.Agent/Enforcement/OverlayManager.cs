using Microsoft.Win32;
using Sandy.Agent.Shell;
using Sandy.Agent.Views;

namespace Sandy.Agent.Enforcement;

public sealed class OverlayManager : IDisposable
{
    private readonly List<ExpiredOverlayWindow> _windows = [];
    private readonly SystemAudioMute _audioMute = new();
    private KeyboardBlocker? _keyboardBlocker;
    private bool _active;

    public OverlayManager() => SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;

    public void Show()
    {
        if (_active)
            return;
        _audioMute.Mute();
        _active = true;
        // Once time has expired, preserve the fullscreen app's process and state
        // but move its surface out of the way before raising the blocking UI.
        if (TopLevelWindowTracker.IsForegroundFullscreen(out var fullscreenWindow, out _))
            TopLevelWindowTracker.Minimize(fullscreenWindow);
        CreateWindows();
        FocusOverlays();
        try
        {
            _keyboardBlocker = new KeyboardBlocker(FocusOverlays);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Keep the blocking overlay visible even if Windows refuses the
            // best-effort keyboard hook in this session.
        }
    }

    public void Hide()
    {
        if (!_active)
            return;
        _active = false;
        foreach (var window in _windows)
            window.CloseForResume();
        _windows.Clear();
        _keyboardBlocker?.Dispose();
        _keyboardBlocker = null;
        _audioMute.Restore();
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
        Hide();
        _audioMute.Dispose();
    }

    private void CreateWindows()
    {
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var window = new ExpiredOverlayWindow(screen.Bounds);
            _windows.Add(window);
            window.Show();
        }
    }

    private void FocusOverlays()
    {
        foreach (var window in _windows)
            window.BringToFront();
    }

    private void DisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (!_active)
            return;
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var window in _windows)
                window.CloseForResume();
            _windows.Clear();
            CreateWindows();
        });
    }
}
