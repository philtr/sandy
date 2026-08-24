using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Threading;

namespace Sandy.Agent.Shell;

public sealed class TaskbarGuardianLease : IDisposable
{
    private readonly string _leasePath;
    private readonly string _readyPath;
    private readonly DispatcherTimer _pulse = new() { Interval = TimeSpan.FromSeconds(1) };
    private Process? _guardian;

    public TaskbarGuardianLease(string root)
    {
        Directory.CreateDirectory(root);
        _leasePath = Path.Combine(root, "taskbar-guardian.lease");
        _readyPath = $"{_leasePath}.ready";
        TryDelete(_readyPath);
        Pulse();
        _pulse.Tick += Pulse;
        _pulse.Start();

        var executable = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(executable))
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--taskbar-guardian");
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add(_leasePath);
            try
            {
                _guardian = Process.Start(startInfo);
                WaitForReady();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                _guardian = null;
                NativeTaskbarController.ShowAll();
            }
        }
    }

    public bool IsHealthy => File.Exists(_readyPath) && _guardian is { HasExited: false };

    public void Dispose()
    {
        _pulse.Stop();
        _pulse.Tick -= Pulse;
        NativeTaskbarController.ShowAll();
        TryDelete(_leasePath);
        TryDelete(_readyPath);
        _guardian?.Dispose();
    }

    public static int RunGuardian(string[] args)
    {
        if (args.Length < 3
            || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parentId))
            return 2;

        var leasePath = args[2];
        var readyPath = $"{leasePath}.ready";
        try
        {
            if (!File.Exists(leasePath))
                return 2;
            File.WriteAllText(readyPath, Environment.ProcessId.ToString(CultureInfo.InvariantCulture), Encoding.ASCII);
        }
        catch (IOException)
        {
            NativeTaskbarController.ShowAll();
            return 3;
        }

        while (true)
        {
            try
            {
                using var parent = Process.GetProcessById(parentId);
                if (parent.HasExited)
                    break;
            }
            catch (ArgumentException)
            {
                break;
            }

            try
            {
                if (!File.Exists(leasePath)
                    || DateTime.UtcNow - File.GetLastWriteTimeUtc(leasePath) > TimeSpan.FromSeconds(5))
                    break;
            }
            catch (IOException)
            {
                break;
            }

            Thread.Sleep(1000);
        }

        NativeTaskbarController.ShowAll();
        TryDelete(readyPath);
        return 0;
    }

    private void Pulse(object? sender = null, EventArgs? args = null)
    {
        try
        {
            File.WriteAllText(_leasePath, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture), Encoding.ASCII);
        }
        catch (IOException)
        {
            NativeTaskbarController.ShowAll();
        }
    }

    private void WaitForReady()
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (_guardian is { HasExited: false } && !File.Exists(_readyPath) && DateTime.UtcNow < deadline)
            Thread.Sleep(25);
        if (!File.Exists(_readyPath))
            NativeTaskbarController.ShowAll();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // A stale recovery marker cannot justify hiding Explorer.
        }
    }
}
