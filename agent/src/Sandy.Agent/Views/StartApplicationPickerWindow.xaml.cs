using System.Windows;
using System.Windows.Input;
using Sandy.Agent.Launcher;
using Sandy.Core.Launcher;

namespace Sandy.Agent.Views;

public partial class StartApplicationPickerWindow : Window
{
    private readonly IReadOnlyList<LauncherApplicationChoice> _applications;

    public StartApplicationPickerWindow(IReadOnlyList<LauncherApplicationChoice> applications)
    {
        InitializeComponent();
        _applications = applications;
        ApplicationsList.ItemsSource = _applications;
    }

    public LauncherApplicationChoice? SelectedApplication { get; private set; }

    private void FilterText_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var filter = FilterText.Text.Trim();
        ApplicationsList.ItemsSource = string.IsNullOrEmpty(filter)
            ? _applications
            : _applications.Where(item => item.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)).ToArray();
    }

    private void ApplicationsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Select();
    private void Select_Click(object sender, RoutedEventArgs e) => Select();
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Select()
    {
        if (ApplicationsList.SelectedItem is not LauncherApplicationChoice selected)
            return;
        SelectedApplication = selected;
        DialogResult = true;
    }
}
