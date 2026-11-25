using System.ComponentModel;
using AlfaRacing.Core.Launching;
using AlfaRacing.Core.Models;

namespace AlfaRacing.Core.Tests;

public class LauncherTests
{
    private readonly FakeProcessService _processes = new();
    private readonly RecordingDelay _delay = new();
    private readonly HashSet<string> _existingFiles = new(StringComparer.OrdinalIgnoreCase);

    private Launcher CreateLauncher() =>
        new(_processes, _delay.Invoke, path => _existingFiles.Contains(path), TimeSpan.FromMilliseconds(100));

    private LaunchItem Item(string name, string path, int delay = 0, bool enabled = true, bool stopOnExit = true, params string[] processNames)
    {
        if (!PathHelpers.IsUri(path))
        {
            _existingFiles.Add(path);
        }

        return new LaunchItem
        {
            Name = name,
            Path = path,
            DelaySeconds = delay,
            Enabled = enabled,
            StopOnExit = stopOnExit,
            ProcessNames = [.. processNames],
        };
    }

    [Fact]
    public async Task Start_LaunchesEnabledItemsInOrder_AndSkipsDisabledOnes()
    {
        var items = new List<LaunchItem>
        {
            Item("MOZA", @"C:\MOZA\MOZA Pit House.exe"),
            Item("Trading Paints", @"C:\TP\Trading Paints.exe", enabled: false),
            Item("CrewChief", @"C:\CC\CrewChiefV4.exe"),
        };

        var results = await CreateLauncher().StartAllAsync(items);

        Assert.Equal([StartOutcome.Started, StartOutcome.Disabled, StartOutcome.Started], results.Select(r => r.Outcome));
        Assert.Equal([@"C:\MOZA\MOZA Pit House.exe", @"C:\CC\CrewChiefV4.exe"], _processes.Started.Select(s => s.FileName));
    }

    [Fact]
    public async Task Start_UsesExeFolderAsWorkingDirectory_AndPassesArguments()
    {
        var item = Item("CrewChief", @"C:\Program Files (x86)\Britton IT Ltd\CrewChiefV4\CrewChiefV4.exe");
        item.Arguments = "-minimized";

        await CreateLauncher().StartAllAsync([item]);

        var started = Assert.Single(_processes.Started);
        Assert.Equal("-minimized", started.Arguments);
        Assert.Equal(@"C:\Program Files (x86)\Britton IT Ltd\CrewChiefV4", started.WorkingDirectory);
    }

    [Fact]
    public async Task Start_SkipsProgramsThatAreAlreadyRunning()
    {
        _processes.Processes.Add(new FakeProcess("CrewChiefV4"));
        var items = new List<LaunchItem> { Item("CrewChief", @"C:\CC\CrewChiefV4.exe") };

        var results = await CreateLauncher().StartAllAsync(items);

        Assert.Equal(StartOutcome.AlreadyRunning, Assert.Single(results).Outcome);
        Assert.Empty(_processes.Started);
    }

    [Fact]
    public async Task Start_ReportsMissingFiles_AndContinues()
    {
        var missing = new LaunchItem { Name = "Bloops", Path = @"C:\nowhere\Bloops.exe" };
        var items = new List<LaunchItem> { missing, Item("CrewChief", @"C:\CC\CrewChiefV4.exe") };

        var results = await CreateLauncher().StartAllAsync(items);

        Assert.Equal(StartOutcome.NotFound, results[0].Outcome);
        Assert.True(results[0].IsProblem);
        Assert.Equal(StartOutcome.Started, results[1].Outcome);
    }

    [Fact]
    public async Task Start_LaunchesUrisWithoutFileCheckOrWorkingDirectory()
    {
        var items = new List<LaunchItem> { Item("iRacing (Steam)", "steam://rungameid/266410", processNames: "iRacingUI") };

        var results = await CreateLauncher().StartAllAsync(items);

        Assert.Equal(StartOutcome.Started, Assert.Single(results).Outcome);
        var started = Assert.Single(_processes.Started);
        Assert.Equal("steam://rungameid/266410", started.FileName);
        Assert.Null(started.WorkingDirectory);
    }

    [Fact]
    public async Task Start_WaitsOnlyAfterSomethingWasLaunched()
    {
        _processes.Processes.Add(new FakeProcess("CrewChiefV4"));
        var items = new List<LaunchItem>
        {
            Item("MOZA", @"C:\MOZA\MOZA Pit House.exe", delay: 5),      // first launch: no wait
            Item("Trading Paints", @"C:\TP\Trading Paints.exe", delay: 2), // waits 2s
            Item("CrewChief", @"C:\CC\CrewChiefV4.exe", delay: 3),        // already running: no wait
            Item("iRacing", @"C:\iRacing\ui\iRacingUI.exe", delay: 1),     // waits 1s
        };

        await CreateLauncher().StartAllAsync(items);

        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)], _delay.Calls);
    }

    [Fact]
    public async Task Start_ReportsCancelledAdminPrompt()
    {
        _processes.StartError = _ => new Win32Exception(1223);
        var results = await CreateLauncher().StartAllAsync([Item("MOZA", @"C:\MOZA\MOZA Pit House.exe")]);

        Assert.Equal(StartOutcome.Cancelled, Assert.Single(results).Outcome);
    }

    [Fact]
    public async Task Start_ReportsProgress()
    {
        var reports = new List<LaunchProgress>();
        var progress = new SynchronousProgress<LaunchProgress>(reports.Add);

        await CreateLauncher().StartAllAsync([Item("CrewChief", @"C:\CC\CrewChiefV4.exe")], progress);

        Assert.Contains(reports, r => r.ItemName == "CrewChief" && r.Total == 1 && r.Message == "실행하는 중");
    }

    [Fact]
    public void StopTargets_AreReverseStartOrder_ThenExtras_AndRespectStopOnExit()
    {
        var settings = new AppSettings
        {
            StartItems =
            [
                Item("MOZA", @"C:\MOZA\MOZA Pit House.exe"),
                Item("Trading Paints", @"C:\TP\Trading Paints.exe", stopOnExit: false),
                Item("CrewChief", @"C:\CC\CrewChiefV4.exe"),
                Item("iRacing", @"C:\iRacing\ui\iRacingUI.exe", processNames: ["iRacingUI", "iRacingSim*"]),
            ],
            ExtraStopProcesses = ["SimHubWPF", "*"],
        };

        var targets = Launcher.BuildStopTargets(settings);

        Assert.Equal(["iRacing", "CrewChief", "MOZA", "SimHubWPF"], targets.Select(t => t.DisplayName));
        Assert.Equal(2, targets[0].Patterns.Count);
        Assert.Equal("MOZA Pit House", targets[2].Patterns.Single().Text);
    }

    [Fact]
    public async Task Stop_ClosesInReverseOrder_GracefullyWhenPossible()
    {
        var journal = new List<string>();
        _processes.Processes.Add(new FakeProcess("MOZA Pit House", journal: journal));
        _processes.Processes.Add(new FakeProcess("CrewChiefV4", journal: journal));
        _processes.Processes.Add(new FakeProcess("iRacingUI", journal: journal));
        var settings = new AppSettings
        {
            StartItems =
            [
                Item("MOZA", @"C:\MOZA\MOZA Pit House.exe"),
                Item("CrewChief", @"C:\CC\CrewChiefV4.exe"),
                Item("iRacing", @"C:\iRacing\ui\iRacingUI.exe"),
            ],
        };

        var results = await CreateLauncher().StopAllAsync(settings);

        Assert.All(results, r => Assert.Equal(StopOutcome.Stopped, r.Outcome));
        Assert.Equal(["close:iRacingUI", "close:CrewChiefV4", "close:MOZA Pit House"], journal);
    }

    [Fact]
    public async Task Stop_TrayAppWithoutWindow_IsKilledWithoutWaiting()
    {
        var tray = new FakeProcess("Trading Paints", hasWindow: false);
        _processes.Processes.Add(tray);
        var settings = new AppSettings { StartItems = [Item("Trading Paints", @"C:\TP\Trading Paints.exe")] };

        var results = await CreateLauncher().StopAllAsync(settings);

        Assert.Equal(StopOutcome.ForceStopped, Assert.Single(results).Outcome);
        Assert.Equal(1, tray.Kills);
        Assert.Empty(_delay.Calls);
    }

    [Fact]
    public async Task Stop_ProgramIgnoringClose_IsKilledAfterTimeout()
    {
        var stubborn = new FakeProcess("MOZA Pit House", closesOnRequest: false);
        _processes.Processes.Add(stubborn);
        var settings = new AppSettings
        {
            StartItems = [Item("MOZA", @"C:\MOZA\MOZA Pit House.exe")],
            ForceKillAfterSeconds = 3,
        };

        var results = await CreateLauncher().StopAllAsync(settings);

        Assert.Equal(StopOutcome.ForceStopped, Assert.Single(results).Outcome);
        Assert.Equal(1, stubborn.CloseRequests);
        Assert.Equal(1, stubborn.Kills);
        // 3 seconds of 100 ms polls before the kill.
        Assert.Equal(30, _delay.Calls.Count);
    }

    [Fact]
    public async Task Stop_ElevatedProgram_ReportsAccessDenied()
    {
        _processes.Processes.Add(new FakeProcess("MOZA Pit House", hasWindow: false, killDenied: true));
        var settings = new AppSettings { StartItems = [Item("MOZA", @"C:\MOZA\MOZA Pit House.exe")] };

        var result = Assert.Single(await CreateLauncher().StopAllAsync(settings));

        Assert.Equal(StopOutcome.AccessDenied, result.Outcome);
        Assert.True(result.IsProblem);
    }

    [Fact]
    public async Task Stop_NotRunning_And_NoProcessNames()
    {
        var settings = new AppSettings
        {
            StartItems =
            [
                Item("CrewChief", @"C:\CC\CrewChiefV4.exe"),
                Item("Shortcut", @"C:\Users\me\Desktop\Something.lnk"),
            ],
        };

        var results = await CreateLauncher().StopAllAsync(settings);

        Assert.Equal([StopOutcome.NoProcessNames, StopOutcome.NotRunning], results.Select(r => r.Outcome));
    }

    [Fact]
    public async Task Stop_NeverTouchesProtectedProcesses()
    {
        var explorer = new FakeProcess("explorer", hasWindow: false);
        _processes.Processes.Add(explorer);
        var settings = new AppSettings { ExtraStopProcesses = ["explorer.exe"] };

        var result = Assert.Single(await CreateLauncher().StopAllAsync(settings));

        Assert.Equal(StopOutcome.NotRunning, result.Outcome);
        Assert.Equal(0, explorer.Kills);
        Assert.Equal(0, explorer.CloseRequests);
    }

    [Fact]
    public async Task Stop_ElectronAppWithHelperProcesses_KillsTheRest()
    {
        var main = new FakeProcess("irdashies", hasWindow: true, closesOnRequest: false);
        var helper1 = new FakeProcess("irdashies", hasWindow: false);
        var helper2 = new FakeProcess("irdashies", hasWindow: false);
        _processes.Processes.AddRange([main, helper1, helper2]);
        var settings = new AppSettings { StartItems = [Item("irDashies", @"C:\Users\me\AppData\Local\irdashies\irdashies.exe")] };

        var result = Assert.Single(await CreateLauncher().StopAllAsync(settings));

        Assert.Equal(StopOutcome.ForceStopped, result.Outcome);
        Assert.True(main.HasExited && helper1.HasExited && helper2.HasExited);
    }

    [Fact]
    public void IsAnyRunning_SupportsPrefixPatterns()
    {
        _processes.Processes.Add(new FakeProcess("iRacingSim64DX11"));
        var launcher = CreateLauncher();

        Assert.True(launcher.IsAnyRunning(["iRacingSim*"]));
        Assert.False(launcher.IsAnyRunning(["iRacingUI"]));
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
