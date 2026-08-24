using System.Windows;

namespace Sandy.Agent.Views;

public partial class CountdownWindow : Window
{
    public CountdownWindow(System.Windows.Forms.Screen screen)
    {
        InitializeComponent();

        SourceInitialized += (_, _) => ForegroundNoticePosition.Apply(this, screen);
    }

    public void Update(TimeSpan remaining) => RemainingText.Text = TimeText.Format(remaining);
}
