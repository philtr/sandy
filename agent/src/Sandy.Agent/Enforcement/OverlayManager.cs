using Microsoft.Win32;
using Sandy.Agent.Views;

namespace Sandy.Agent.Enforcement;

public sealed class OverlayManager : IDisposable
{
    private readonly List<ExpiredOverlayWindow> _windows = [];
    private KeyboardBlocker? _keyboardBlocker;
    private bool _active;

    public OverlayManager() => SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;

    public void Show()
    {
        if (_active)
            return;
        _active = true;
        _keyboardBlocker = new KeyboardBlocker();
        CreateWindows();
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
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
        Hide();
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
