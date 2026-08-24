using System.Windows;

namespace Sandy.Agent.Views;

public partial class CountdownWindow : Window
{
    private const double ScreenMargin = 20;

    public CountdownWindow()
    {
        InitializeComponent();

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - ScreenMargin;
        Top = workArea.Top + ScreenMargin;
    }

    public void Update(TimeSpan remaining) => RemainingText.Text = StatusWindow.Format(remaining);
}
