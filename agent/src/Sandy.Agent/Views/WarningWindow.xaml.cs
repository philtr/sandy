using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;

namespace Sandy.Agent.Views;

public partial class WarningWindow : Window
{
    private static readonly TimeSpan AutoDismissAfter = TimeSpan.FromSeconds(10);
    private readonly Stopwatch _visibleFor = new();
    private readonly DispatcherTimer _dismissTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(100)
    };

    public WarningWindow(string message, System.Windows.Forms.Screen screen)
    {
        InitializeComponent();
        MessageText.Text = message;
        _dismissTimer.Tick += DismissTimer_Tick;
        SourceInitialized += (_, _) => ForegroundNoticePosition.Apply(this, screen);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        _visibleFor.Start();
        _dismissTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _dismissTimer.Stop();
        _dismissTimer.Tick -= DismissTimer_Tick;
        base.OnClosed(e);
    }

    private void DismissTimer_Tick(object? sender, EventArgs e)
    {
        var remaining = AutoDismissAfter - _visibleFor.Elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            Close();
            return;
        }

        CountdownText.Text = Math.Ceiling(remaining.TotalSeconds).ToString(CultureInfo.InvariantCulture);
    }
}
