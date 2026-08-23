using System.Windows;
using System.Windows.Media;
using Sandy.Core.Sync;
using Sandy.Core.Time;

namespace Sandy.Agent.Views;

public partial class StatusWindow : Window
{
    public StatusWindow() => InitializeComponent();

    public void UpdateTimer(TimerReading reading)
    {
        RemainingText.Text = reading.HasAuthoritativeState
            ? reading.Phase == TimerPhase.Expired ? "Time’s up" : Format(reading.Remaining)
            : "Checking…";
    }

    public void UpdateConnection(ConnectionState state)
    {
        ConnectionText.Text = state.ToString();
        ConnectionDot.Fill = new SolidColorBrush(state switch
        {
            ConnectionState.Online => System.Windows.Media.Color.FromRgb(88, 214, 141),
            ConnectionState.Offline => System.Windows.Media.Color.FromRgb(255, 138, 128),
            _ => System.Windows.Media.Color.FromRgb(244, 185, 66)
        });
    }

    public static string Format(TimeSpan remaining)
    {
        var total = Math.Max(0, (long)Math.Ceiling(remaining.TotalSeconds));
        return total >= 3600
            ? $"{total / 3600}:{total % 3600 / 60:00}:{total % 60:00}"
            : $"{total / 60}:{total % 60:00}";
    }
}
