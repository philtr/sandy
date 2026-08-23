using System.Windows;

namespace Sandy.Agent.Views;

public partial class WarningWindow : Window
{
    public WarningWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
