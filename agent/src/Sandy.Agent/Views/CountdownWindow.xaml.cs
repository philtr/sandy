using System.Windows;

namespace Sandy.Agent.Views;

public partial class CountdownWindow : Window
{
    public CountdownWindow() => InitializeComponent();
    public void Update(TimeSpan remaining) => RemainingText.Text = StatusWindow.Format(remaining);
}
