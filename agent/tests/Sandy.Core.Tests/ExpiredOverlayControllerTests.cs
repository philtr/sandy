using Sandy.Core.Enforcement;

namespace Sandy.Core.Tests;

public sealed class ExpiredOverlayControllerTests
{
    [Fact]
    public void Show_retries_minimization_and_focus_when_game_reclaims_foreground()
    {
        var desktop = new FakeDesktop { Foreground = ForegroundWindow.Game };
        var controller = new ExpiredOverlayController(desktop);

        controller.Show();
        Assert.Equal(ForegroundWindow.Overlay, desktop.Foreground);

        desktop.Foreground = ForegroundWindow.Game;
        controller.Show();

        Assert.Equal(2, desktop.MinimizeCalls);
        Assert.Equal(1, desktop.ShowCalls);
        Assert.Equal(2, desktop.FocusCalls);
        Assert.Equal(ForegroundWindow.Overlay, desktop.Foreground);
    }

    [Fact]
    public void Show_retries_minimization_when_first_request_is_asynchronous()
    {
        var desktop = new FakeDesktop
        {
            Foreground = ForegroundWindow.Game,
            DelayFirstMinimize = true
        };
        var controller = new ExpiredOverlayController(desktop);

        controller.Show();
        Assert.Equal(ForegroundWindow.Game, desktop.Foreground);

        controller.Show();

        Assert.Equal(2, desktop.MinimizeCalls);
        Assert.Equal(2, desktop.FocusCalls);
        Assert.Equal(1, desktop.ShowCalls);
        Assert.Equal(ForegroundWindow.Overlay, desktop.Foreground);
    }

    [Fact]
    public void First_show_minimizes_before_showing_overlays()
    {
        var desktop = new FakeDesktop { Foreground = ForegroundWindow.Game };
        var controller = new ExpiredOverlayController(desktop);

        controller.Show();

        Assert.Equal(new[] { "minimize", "show", "focus" }, desktop.Calls);
    }

    [Fact]
    public void Show_does_not_refocus_when_overlay_already_has_foreground()
    {
        var desktop = new FakeDesktop { Foreground = ForegroundWindow.Overlay };
        var controller = new ExpiredOverlayController(desktop);

        controller.Show();
        controller.Show();

        Assert.Equal(0, desktop.FocusCalls);
    }

    [Fact]
    public void Hide_stops_enforcement_until_next_show()
    {
        var desktop = new FakeDesktop { Foreground = ForegroundWindow.Game };
        var controller = new ExpiredOverlayController(desktop);
        controller.Show();
        Assert.True(controller.IsActive);
        controller.Hide();
        var callsAfterHide = desktop.Calls.Count;
        Assert.False(controller.IsActive);
        Assert.Equal(1, desktop.HideCalls);

        controller.Hide();
        Assert.Equal(callsAfterHide, desktop.Calls.Count);

        controller.Show();

        Assert.Equal(callsAfterHide + 3, desktop.Calls.Count);
        Assert.Equal(2, desktop.ShowCalls);
        Assert.Equal(1, desktop.HideCalls);
        Assert.True(controller.IsActive);
    }

    [Fact]
    public void Failed_show_is_retried_on_next_call()
    {
        var desktop = new FakeDesktop { FailNextShow = true, Foreground = ForegroundWindow.Game };
        var controller = new ExpiredOverlayController(desktop);

        Assert.Throws<InvalidOperationException>(() => controller.Show());
        controller.Show();

        Assert.Equal(2, desktop.ShowCalls);
        Assert.True(controller.IsActive);
    }

    private sealed class FakeDesktop : IExpiredOverlayDesktop
    {
        public List<string> Calls { get; } = [];
        public int MinimizeCalls { get; private set; }
        public int ShowCalls { get; private set; }
        public int FocusCalls { get; private set; }
        public int HideCalls { get; private set; }
        public bool OverlayHasForeground => Foreground == ForegroundWindow.Overlay;
        public bool FailNextShow { get; set; }
        public bool DelayFirstMinimize { get; set; }
        public ForegroundWindow Foreground { get; set; }

        public void MinimizeForegroundFullscreen()
        {
            MinimizeCalls++;
            Calls.Add("minimize");
            if (DelayFirstMinimize && MinimizeCalls == 1)
                return;
            if (Foreground == ForegroundWindow.Game)
                Foreground = ForegroundWindow.Background;
        }

        public void ShowOverlays()
        {
            ShowCalls++;
            Calls.Add("show");
            if (FailNextShow)
            {
                FailNextShow = false;
                throw new InvalidOperationException("simulated show failure");
            }
        }

        public void FocusOverlays()
        {
            FocusCalls++;
            Calls.Add("focus");
            if (Foreground != ForegroundWindow.Game)
                Foreground = ForegroundWindow.Overlay;
        }

        public void HideOverlays()
        {
            HideCalls++;
            Calls.Add("hide");
            if (Foreground == ForegroundWindow.Overlay)
                Foreground = ForegroundWindow.Background;
        }
    }

    private enum ForegroundWindow { Background, Game, Overlay }
}
