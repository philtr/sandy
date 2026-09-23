namespace Sandy.Core.Enforcement;

public interface IExpiredOverlayDesktop
{
    void MinimizeForegroundFullscreen();
    void ShowOverlays();
    bool OverlayHasForeground { get; }
    void FocusOverlays();
    void HideOverlays();
}

public sealed class ExpiredOverlayController
{
    private readonly IExpiredOverlayDesktop _desktop;

    public ExpiredOverlayController(IExpiredOverlayDesktop desktop) => _desktop = desktop;

    public bool IsActive { get; private set; }

    public void Show()
    {
        _desktop.MinimizeForegroundFullscreen();
        if (!IsActive)
        {
            _desktop.ShowOverlays();
            IsActive = true;
        }

        if (!_desktop.OverlayHasForeground)
            _desktop.FocusOverlays();
    }

    public void Hide()
    {
        if (!IsActive)
            return;

        IsActive = false;
        _desktop.HideOverlays();
    }
}
