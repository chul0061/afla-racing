using System.ComponentModel;
using System.Diagnostics;

namespace AlfaRacing.Core.Processes;

/// <summary>A running process ALFA Racing may close.</summary>
public interface IRunningProcess : IDisposable
{
    int Id { get; }

    string Name { get; }

    bool HasExited { get; }

    /// <summary>Asks the program to close its main window. False when it has no window (e.g. tray apps).</summary>
    bool RequestClose();

    /// <summary>Force-kills the process and its child processes.</summary>
    void Kill();
}

public interface IProcessService
{
    /// <summary>Names of all running processes (without ".exe").</summary>
    IReadOnlyList<string> GetRunningProcessNames();

    /// <summary>Running processes matching any pattern, excluding protected system processes and ALFA Racing itself.</summary>
    IReadOnlyList<IRunningProcess> FindProcesses(IReadOnlyList<ProcessNamePattern> patterns);

    /// <summary>Starts a program the way Explorer would (works for exe, shortcuts, URIs and admin prompts).</summary>
    void Start(string fileName, string arguments, string? workingDirectory);
}

public sealed class SystemProcessService : IProcessService
{
    private readonly int _ownProcessId = Environment.ProcessId;

    public IReadOnlyList<string> GetRunningProcessNames()
    {
        var processes = Process.GetProcesses();
        var names = new List<string>(processes.Length);
        foreach (var process in processes)
        {
            try
            {
                names.Add(process.ProcessName);
            }
            catch (Exception)
            {
                // Process exited while enumerating.
            }
            finally
            {
                process.Dispose();
            }
        }

        return names;
    }

    public IReadOnlyList<IRunningProcess> FindProcesses(IReadOnlyList<ProcessNamePattern> patterns)
    {
        var result = new List<IRunningProcess>();
        if (patterns.Count == 0)
        {
            return result;
        }

        foreach (var process in Process.GetProcesses())
        {
            var keep = false;
            try
            {
                var name = process.ProcessName;
                keep = process.Id != _ownProcessId
                       && !ProcessGuard.IsProtected(name)
                       && patterns.Any(p => p.Matches(name));
                if (keep)
                {
                    result.Add(new RunningProcess(process, name));
                }
            }
            catch (Exception)
            {
                keep = false;
            }
            finally
            {
                if (!keep)
                {
                    process.Dispose();
                }
            }
        }

        return result;
    }

    public void Start(string fileName, string arguments, string? workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? "",
            UseShellExecute = true,
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        using var _ = Process.Start(startInfo);
    }

    private sealed class RunningProcess(Process process, string name) : IRunningProcess
    {
        public int Id { get; } = process.Id;

        public string Name { get; } = name;

        public bool HasExited
        {
            get
            {
                try
                {
                    return process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
                catch (Win32Exception)
                {
                    // No permission to query: assume it is still running.
                    return false;
                }
            }
        }

        public bool RequestClose()
        {
            try
            {
                return process.CloseMainWindow();
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Kill()
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited.
            }
        }

        public void Dispose() => process.Dispose();
    }
}
