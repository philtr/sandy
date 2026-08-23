using System.Reflection;
using System.Net.Http;
using System.Windows;
using Sandy.Agent.Infrastructure;
using Sandy.Agent.Updates;
using Sandy.Agent.Views;
using Sandy.Core.Configuration;
using Sandy.Core.Events;
using Sandy.Core.Networking;
using Sandy.Core.Persistence;
using Sandy.Core.Sync;
using Sandy.Core.Time;
using Velopack;

namespace Sandy.Agent;

public partial class App : System.Windows.Application
{
    private const string DefaultUpdateRepository = "https://github.com/philtr/sandy";
    private static Mutex? _singleInstance;
    private readonly CancellationTokenSource _shutdown = new();
    private AgentController? _controller;
    private HttpClient? _httpClient;

    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        _singleInstance = new Mutex(initiallyOwned: true, "Local\\Sandy.Agent", out var createdNew);
        if (!createdNew)
            return;

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            StartupRegistration.EnsureRegistered();
        }
        catch
        {
            // Velopack also registers installed apps; a locked-down Run key should not
            // prevent the timer from starting in the current session.
        }

        var paths = AgentPaths.ForCurrentUser();
        var credentialStore = new DpapiDeviceCredentialStore(paths.CredentialFile);
        ISnapshotStore snapshotStore = new SnapshotStore(paths.SnapshotFile);
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"Sandy-Agent/{AgentVersion()}");
        ISandyApiClient api = new SandyApiClient(_httpClient);

        var credential = await credentialStore.LoadAsync(_shutdown.Token);
        if (credential is null)
        {
            var enrollment = new EnrollmentWindow(
                new EnrollmentService(api, credentialStore, snapshotStore), AgentVersion());
            if (enrollment.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
            credential = await credentialStore.LoadAsync(_shutdown.Token);
        }

        if (credential is null)
        {
            System.Windows.MessageBox.Show("Enrollment did not save a device credential.", "Sandy", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var options = new AgentOptions
        {
            ServerUri = credential.ServerUri,
            AgentVersion = AgentVersion()
        };
        var timer = new SynchronizedTimer(new SystemMonotonicClock());
        var synchronization = new TimerSynchronizationService(
            api, new ActionCableStateClient(), credentialStore, snapshotStore, timer, options);
        await synchronization.RestoreCachedStateAsync(_shutdown.Token);

        var eventQueue = new DeviceEventQueue(api, credentialStore);
        var updateUrl = Environment.GetEnvironmentVariable("SANDY_UPDATE_URL");
        IUpdateService updateService = new VelopackUpdateService(
            string.IsNullOrWhiteSpace(updateUrl) ? DefaultUpdateRepository : updateUrl);

        var statusWindow = new StatusWindow();
        _controller = new AgentController(timer, synchronization, eventQueue, updateService, statusWindow);
        _controller.Start();
        eventQueue.TryEnqueue("agent_started", new Dictionary<string, object?> { ["version"] = AgentVersion() });

        RunBackground(synchronization.RunAsync(_shutdown.Token));
        RunBackground(eventQueue.RunAsync(_shutdown.Token));
        RunBackground(_controller.RunUpdateChecksAsync(_shutdown.Token));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shutdown.Cancel();
        _controller?.Dispose();
        _httpClient?.Dispose();
        _singleInstance?.Dispose();
        _shutdown.Dispose();
        base.OnExit(e);
    }

    private static string AgentVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    private static async void RunBackground(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Normal application shutdown.
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError(exception.ToString());
        }
    }
}
