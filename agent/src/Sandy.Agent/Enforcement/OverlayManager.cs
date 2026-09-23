using Microsoft.Win32;
using System.Windows.Interop;
using Sandy.Agent.Shell;
using Sandy.Agent.Views;
using Sandy.Core.Enforcement;

namespace Sandy.Agent.Enforcement;

public sealed class OverlayManager : IDisposable, IExpiredOverlayDesktop
{
    private readonly List<ExpiredOverlayWindow> _windows = [];
    private readonly SystemAudioMute _audioMute = new();
    private readonly ExpiredOverlayController _controller;
    private KeyboardBlocker? _keyboardBlocker;

    public OverlayManager()
    {
        _controller = new ExpiredOverlayController(this);
        SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
    }

    public void Show() => _controller.Show();

    public void Hide() => _controller.Hide();

    bool IExpiredOverlayDesktop.OverlayHasForeground =>
        _windows.Any(window => window.HasForeground)
        // Re-enrollment must remain usable while an unknown credential fails closed.
        || System.Windows.Application.Current.Windows.OfType<EnrollmentWindow>().Any(window =>
            window.IsVisible && TopLevelWindowTracker.IsForegroundWindow(new WindowInteropHelper(window).Handle));

    void IExpiredOverlayDesktop.MinimizeForegroundFullscreen()
    {
        if (TopLevelWindowTracker.IsForegroundFullscreen(out var fullscreenWindow, out _))
            TopLevelWindowTracker.Minimize(fullscreenWindow);
    }

    void IExpiredOverlayDesktop.ShowOverlays()
    {
        _audioMute.Mute();
        CreateWindows();
        try
        {
            _keyboardBlocker = new KeyboardBlocker(RecoverFocus);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Keep the blocking overlay visible if Windows rejects the keyboard hook.
        }
    }

    void IExpiredOverlayDesktop.HideOverlays()
    {
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

    void IExpiredOverlayDesktop.FocusOverlays()
    {
        foreach (var window in _windows)
            window.BringToFront();
    }

    private void RecoverFocus()
    {
        if (_controller.IsActive)
            _controller.Show();
    }

    private void DisplaySettingsChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (!_controller.IsActive)
                return;
            foreach (var window in _windows)
                window.CloseForResume();
            _windows.Clear();
            CreateWindows();
            RecoverFocus();
        });
    }
}
