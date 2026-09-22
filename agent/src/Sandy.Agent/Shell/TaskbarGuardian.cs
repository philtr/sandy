using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Threading;
using Sandy.Core.Recovery;

namespace Sandy.Agent.Shell;

public sealed class TaskbarGuardianLease : IDisposable
{
    private readonly string _root;
    private readonly int _recoveryAttempt;
    private string _leasePath = string.Empty;
    private string _readyPath = string.Empty;
    private readonly DispatcherTimer _pulse = new() { Interval = TimeSpan.FromSeconds(1) };
    private Process? _guardian;
    private bool _stopping;
    private bool _disposed;

    public TaskbarGuardianLease(string root, int recoveryAttempt = 0)
    {
        _root = root;
        _recoveryAttempt = recoveryAttempt;
        Directory.CreateDirectory(root);
        _pulse.Tick += Pulse;
        StartGuardian();
    }

    private void StartGuardian()
    {
        _guardian?.Dispose();
        _guardian = null;
        // Each guardian owns its files, even if a replacement starts before it exits.
        _leasePath = Path.Combine(_root, $"taskbar-guardian-{Environment.ProcessId}-{Guid.NewGuid():N}.lease");
        _readyPath = $"{_leasePath}.ready";
        _stopping = false;
        Pulse();
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
            startInfo.ArgumentList.Add(_recoveryAttempt.ToString(CultureInfo.InvariantCulture));
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

    public void PrepareForExit()
    {
        _stopping = true;
        _pulse.Stop();
        TryDelete(_leasePath);
        TryDelete(_readyPath);
        NativeTaskbarController.ShowAll();
    }

    public void RunWithoutRecovery(Action exit)
    {
        PrepareForExit();
        try
        {
            exit();
        }
        finally
        {
            // If update handoff fails or returns, this process still needs a guardian.
            if (!_disposed)
                StartGuardian();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        PrepareForExit();
        _pulse.Tick -= Pulse;
        _guardian?.Dispose();
    }

    public static int RunGuardian(string[] args)
    {
        if (args.Length < 4
            || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parentId)
            || !int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var recoveryAttempt))
            return 2;

        var leasePath = args[2];
        var readyPath = $"{leasePath}.ready";
        var uptime = Stopwatch.StartNew();
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

        var taskbarRestored = false;
        var restart = false;
        while (true)
        {
            var parentRunning = false;
            try
            {
                using var parent = Process.GetProcessById(parentId);
                parentRunning = !parent.HasExited;
            }
            catch (ArgumentException)
            {
                // The process exited between polls.
            }

            GuardianAction action;
            try
            {
                action = AgentRecoveryPolicy.Evaluate(parentRunning, File.Exists(leasePath),
                    DateTime.UtcNow - File.GetLastWriteTimeUtc(leasePath));
            }
            catch (IOException)
            {
                break;
            }

            if (action == GuardianAction.Stop)
            {
                // The owner already restored Explorer. Do not change taskbars after
                // an update or a new guardian has taken over.
                TryDelete(readyPath);
                return 0;
            }
            if (action == GuardianAction.RestartAgent)
            {
                restart = true;
                break;
            }
            if (action == GuardianAction.RestoreTaskbar && !taskbarRestored)
            {
                NativeTaskbarController.ShowAll();
                TryDelete(readyPath);
                taskbarRestored = true;
            }
            else if (action == GuardianAction.Wait && taskbarRestored)
            {
                try
                {
                    File.WriteAllText(readyPath, Environment.ProcessId.ToString(CultureInfo.InvariantCulture), Encoding.ASCII);
                    taskbarRestored = false;
                }
                catch (IOException)
                {
                    break;
                }
            }

            Thread.Sleep(1000);
        }

        NativeTaskbarController.ShowAll();
        TryDelete(readyPath);
        var nextAttempt = AgentRecoveryPolicy.NextRecoveryAttempt(recoveryAttempt, uptime.Elapsed);
        if (restart && nextAttempt is not null && Environment.ProcessPath is { } executable)
        {
            Thread.Sleep(2000);
            if (File.Exists(leasePath))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("--recovery-attempt");
                startInfo.ArgumentList.Add(nextAttempt.Value.ToString(CultureInfo.InvariantCulture));
                try
                {
                    using var replacement = Process.Start(startInfo);
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Explorer is already restored if the replacement cannot start.
                }
            }
        }
        TryDelete(leasePath);
        return 0;
    }

    private void Pulse(object? sender = null, EventArgs? args = null)
    {
        if (_stopping)
            return;
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
