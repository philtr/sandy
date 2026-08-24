using System.Net;
using System.Net.Http;
using System.Windows;
using Sandy.Core.Sync;

namespace Sandy.Agent.Views;

public partial class EnrollmentWindow : Window
{
    private readonly EnrollmentService _enrollmentService;
    private readonly string _agentVersion;
    private readonly bool _recovery;

    public EnrollmentWindow(EnrollmentService enrollmentService, string agentVersion, bool recovery = false)
    {
        InitializeComponent();
        _enrollmentService = enrollmentService;
        _agentVersion = agentVersion;
        _recovery = recovery;
        DeviceName.Text = Environment.MachineName;
        if (recovery)
        {
            Title = "Reconnect Sandy";
            Topmost = true;
        }
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        if (!Uri.TryCreate(ServerUrl.Text.Trim(), UriKind.Absolute, out var serverUri)
            || serverUri.Scheme is not ("https" or "http"))
        {
            ErrorText.Text = "Enter a valid HTTP or HTTPS server address.";
            return;
        }

        ConnectButton.IsEnabled = false;
        try
        {
            await _enrollmentService.EnrollAsync(
                serverUri, JoinCode.Text, DeviceName.Text, _agentVersion);
            DialogResult = true;
            Close();
        }
        catch (Exception exception) when (exception is HttpRequestException or WebException or ArgumentException
                                              or Sandy.Core.Networking.SandyApiException)
        {
            ErrorText.Text = $"Could not connect: {exception.Message}";
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }
}
