using System.ComponentModel;
using AflaRacing.Core.Processes;

namespace AflaRacing.Core.Tests;

internal sealed class FakeProcess : IRunningProcess
{
    private static int _nextId = 10_000;
    private readonly bool _hasWindow;
    private readonly bool _closesOnRequest;
    private readonly bool _killDenied;
    private readonly List<string>? _journal;

    public FakeProcess(string name, bool hasWindow = true, bool closesOnRequest = true, bool killDenied = false, List<string>? journal = null)
    {
        Name = name;
        _hasWindow = hasWindow;
        _closesOnRequest = closesOnRequest;
        _killDenied = killDenied;
        _journal = journal;
    }

    public int Id { get; } = Interlocked.Increment(ref _nextId);

    public string Name { get; }

    public bool HasExited { get; set; }

    public int CloseRequests { get; private set; }

    public int Kills { get; private set; }

    public bool RequestClose()
    {
        CloseRequests++;
        _journal?.Add($"close:{Name}");
        if (!_hasWindow)
        {
            return false;
        }

        if (_closesOnRequest)
        {
            HasExited = true;
        }

        return true;
    }

    public void Kill()
    {
        Kills++;
        _journal?.Add($"kill:{Name}");
        if (_killDenied)
        {
            throw new Win32Exception(5, "Access is denied.");
        }

        HasExited = true;
    }

    public void Dispose()
    {
    }
}

internal sealed class FakeProcessService : IProcessService
{
    public List<FakeProcess> Processes { get; } = [];

    public List<(string FileName, string Arguments, string? WorkingDirectory)> Started { get; } = [];

    public Func<string, Exception?>? StartError { get; set; }

    public IReadOnlyList<string> GetRunningProcessNames() =>
        Processes.Where(p => !p.HasExited).Select(p => p.Name).ToList();

    public IReadOnlyList<IRunningProcess> FindProcesses(IReadOnlyList<ProcessNamePattern> patterns) =>
        Processes.Where(p => !p.HasExited && patterns.Any(x => x.Matches(p.Name))).Cast<IRunningProcess>().ToList();

    public void Start(string fileName, string arguments, string? workingDirectory)
    {
        var error = StartError?.Invoke(fileName);
        if (error is not null)
        {
            throw error;
        }

        Started.Add((fileName, arguments, workingDirectory));
    }
}

internal sealed class RecordingDelay
{
    public List<TimeSpan> Calls { get; } = [];

    public Task Invoke(TimeSpan delay, CancellationToken cancellationToken)
    {
        Calls.Add(delay);
        return Task.CompletedTask;
    }
}

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "afla-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string CreateFile(params string[] relativeParts)
    {
        var full = System.IO.Path.Combine([Path, .. relativeParts]);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "");
        return full;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (Exception)
        {
            // Best effort.
        }
    }
}
