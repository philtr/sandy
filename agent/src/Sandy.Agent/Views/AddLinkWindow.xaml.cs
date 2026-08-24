using System.Windows;
using System.IO;
using Sandy.Core.Launcher;

namespace Sandy.Agent.Views;

public partial class AddLinkWindow : Window
{
    public AddLinkWindow() => InitializeComponent();

    public LauncherPin? Pin { get; private set; }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Pin = new LauncherPin(Guid.NewGuid(), NameText.Text, LauncherPinKind.Uri, UriText.Text).Validate();
            DialogResult = true;
        }
        catch (InvalidDataException exception)
        {
            ErrorText.Text = exception.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
