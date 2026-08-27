using System.Reflection;
using System.Net.Http;
using System.Windows;
using Sandy.Agent.Infrastructure;
using Sandy.Agent.Launcher;
using Sandy.Agent.Shell;
using Sandy.Agent.Updates;
using Sandy.Agent.Views;
using Sandy.Core.Configuration;
using Sandy.Core.Events;
using Sandy.Core.Launcher;
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
    private CancellationTokenSource? _sessionShutdown;
    private AgentController? _controller;
    private HttpClient? _httpClient;
    private DpapiDeviceCredentialStore? _credentialStore;
    private SnapshotStore? _snapshotStore;
    private SandyApiClient? _api;
    private AgentPaths? _paths;
    private TaskbarGuardianLease? _guardian;
    private bool _recoveringEnrollment;

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--taskbar-guardian")
        {
            Environment.ExitCode = TaskbarGuardianLease.RunGuardian(args);
            return;
        }

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

        _paths = AgentPaths.ForCurrentUser();
        _credentialStore = new DpapiDeviceCredentialStore(_paths.CredentialFile);
        _snapshotStore = new SnapshotStore(_paths.SnapshotFile);
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"Sandy-Agent/{AgentVersion()}");
        _api = new SandyApiClient(_httpClient);

        var credential = await _credentialStore.LoadAsync(_shutdown.Token);
        if (credential is null)
        {
            var enrollment = new EnrollmentWindow(
                new EnrollmentService(_api, _credentialStore, _snapshotStore), AgentVersion());
            if (enrollment.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
            credential = await _credentialStore.LoadAsync(_shutdown.Token);
        }

        if (credential is null)
        {
            System.Windows.MessageBox.Show("Enrollment did not save a device credential.", "Sandy", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        credential = await UpgradeLegacyEnrollmentAsync(credential, _shutdown.Token);
        _guardian = new TaskbarGuardianLease(_paths.Root);
        await StartEnrolledSessionAsync(credential, _shutdown.Token);
    }

    private async Task StartEnrolledSessionAsync(DeviceCredential credential, CancellationToken cancellationToken)
    {
        if (_api is null || _credentialStore is null || _snapshotStore is null || _paths is null || _guardian is null)
            throw new InvalidOperationException("Sandy has not finished initializing.");

        _sessionShutdown?.Dispose();
        _sessionShutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sessionToken = _sessionShutdown.Token;
        var options = new AgentOptions
        {
            ServerUri = credential.ServerUri,
            AgentVersion = AgentVersion()
        };
        var timer = new SynchronizedTimer(new SystemMonotonicClock());
        var synchronization = new TimerSynchronizationService(
            _api, new ActionCableStateClient(), _credentialStore, _snapshotStore, timer, options);
        synchronization.EnrollmentRejected += Synchronization_EnrollmentRejected;
        await synchronization.RestoreCachedStateAsync(sessionToken);

        var eventQueue = new DeviceEventQueue(_api, _credentialStore);
        var updateUrl = Environment.GetEnvironmentVariable("SANDY_UPDATE_URL");
        IUpdateService updateService = new VelopackUpdateService(
            string.IsNullOrWhiteSpace(updateUrl) ? DefaultUpdateRepository : updateUrl,
            AcceptPrereleaseUpdates());

        var pinStore = new LauncherPinStore(_paths.LauncherPinsFile);
        var pins = await pinStore.LoadAsync(sessionToken);
        var launcher = new LauncherDesktopManager(
            pinStore, pins, _guardian, new LauncherIconCache(_paths.LauncherIconCacheDirectory));
        _controller = new AgentController(
            timer, synchronization, eventQueue, updateService, options.UpdateCheckInterval, launcher);
        _controller.Start();
        eventQueue.TryEnqueue("agent_started", new Dictionary<string, object?> { ["version"] = AgentVersion() });

        RunBackground(synchronization.RunAsync(sessionToken));
        RunBackground(eventQueue.RunAsync(sessionToken));
        RunBackground(_controller.RunUpdateChecksAsync(sessionToken));
    }

    private async Task<DeviceCredential> UpgradeLegacyEnrollmentAsync(
        DeviceCredential credential,
        CancellationToken cancellationToken)
    {
        if (credential.EnrollmentGeneration != Guid.Empty || _snapshotStore is null || _credentialStore is null)
            return credential;
        var cached = await _snapshotStore.LoadAsync(cancellationToken);
        var upgraded = credential with { EnrollmentGeneration = Guid.NewGuid() };
        await _credentialStore.SaveAsync(upgraded, cancellationToken);
        if (cached is not null)
            await _snapshotStore.SaveAsync(cached.Snapshot, upgraded.EnrollmentGeneration, cancellationToken);
        return upgraded;
    }

    private void Synchronization_EnrollmentRejected(object? sender, EnrollmentFailureEventArgs e) =>
        Dispatcher.BeginInvoke(async () => await RecoverEnrollmentAsync(e.Reason));

    private async Task RecoverEnrollmentAsync(EnrollmentFailureReason reason)
    {
        if (_recoveringEnrollment || _api is null || _credentialStore is null || _snapshotStore is null)
            return;
        _recoveringEnrollment = true;
        try
        {
            if (reason == EnrollmentFailureReason.Revoked)
            {
                _sessionShutdown?.Cancel();
                _controller?.Dispose();
                _controller = null;
                await _credentialStore.ClearAsync(_shutdown.Token);
                await _snapshotStore.ClearAsync(_shutdown.Token);
                NativeTaskbarController.ShowAll();
            }

            var enrollment = new EnrollmentWindow(
                new EnrollmentService(_api, _credentialStore, _snapshotStore), AgentVersion(), recovery: true);
            if (enrollment.ShowDialog() != true)
            {
                if (reason == EnrollmentFailureReason.Revoked)
                    Shutdown();
                return;
            }
            var credential = await _credentialStore.LoadAsync(_shutdown.Token);
            if (credential is null)
                throw new InvalidOperationException("Re-enrollment did not save a credential.");
            _sessionShutdown?.Cancel();
            _controller?.Dispose();
            _controller = null;
            await StartEnrolledSessionAsync(credential, _shutdown.Token);
        }
        finally
        {
            _recoveringEnrollment = false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shutdown.Cancel();
        _sessionShutdown?.Cancel();
        _controller?.Dispose();
        _guardian?.Dispose();
        _httpClient?.Dispose();
        _singleInstance?.Dispose();
        _sessionShutdown?.Dispose();
        _shutdown.Dispose();
        base.OnExit(e);
    }

    private static string AgentVersion() =>
        InformationalVersion().Split('+')[0];

    private static bool AcceptPrereleaseUpdates() => InformationalVersion().Contains('-');

    private static string InformationalVersion() =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

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
