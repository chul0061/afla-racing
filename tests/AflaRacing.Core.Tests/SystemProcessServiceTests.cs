using System.Diagnostics;
using AflaRacing.Core.Launching;
using AflaRacing.Core.Processes;

namespace AflaRacing.Core.Tests;

/// <summary>Runs a real process (Linux/macOS only) to check start, detection and force-kill end to end.</summary>
public class SystemProcessServiceTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task StartDetectAndStop_RealProcess()
    {
        if (OperatingSystem.IsWindows() || !File.Exists("/bin/sleep"))
        {
            return;
        }

        // A unique, short (<= 15 chars) process name so nothing else can match.
        var name = "aflasleep" + Random.Shared.Next(1000, 9999);
        var exe = Path.Combine(_temp.Path, name);
        File.Copy("/bin/sleep", exe);
        File.SetUnixFileMode(exe, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var service = new SystemProcessService();
        Assert.True(ProcessNamePattern.TryParse(name, out var pattern, out _));

        service.Start(exe, "30", _temp.Path);
        var stopwatch = Stopwatch.StartNew();
        while (!service.GetRunningProcessNames().Contains(name) && stopwatch.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(50);
        }

        Assert.Contains(name, service.GetRunningProcessNames());

        var launcher = new Launcher(service);
        var result = await launcher.StopAsync(new StopTarget(name, [pattern]), TimeSpan.FromSeconds(1));

        Assert.Equal(StopOutcome.ForceStopped, result.Outcome); // No window on Linux, so it is killed.
        Assert.DoesNotContain(name, service.GetRunningProcessNames());
    }
}
