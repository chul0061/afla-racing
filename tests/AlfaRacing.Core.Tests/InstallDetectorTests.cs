using AlfaRacing.Core.Catalog;
using AlfaRacing.Core.Detection;

namespace AlfaRacing.Core.Tests;

public class InstallDetectorTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static KnownProgram Program(string[] exeNames, string[]? searchDirectories = null, string[]? uninstallNames = null) => new()
    {
        Id = "test",
        Name = "Test",
        Category = ProgramCategory.Recommended,
        Description = "",
        WebsiteUrl = "https://example.invalid/",
        ExeNames = exeNames,
        SearchDirectories = searchDirectories ?? [],
        UninstallNames = uninstallNames ?? [],
    };

    private string ExpandTestRoot(string value) => value.Replace("%TEST_ROOT%", _temp.Path);

    [Fact]
    public void FindsProgramInItsUsualFolder_WithEnvironmentVariables()
    {
        var exe = _temp.CreateFile("Britton IT Ltd", "CrewChiefV4", "CrewChiefV4.exe");
        var program = Program(["CrewChiefV4.exe"], [Path.Combine("%TEST_ROOT%", "Britton IT Ltd", "CrewChiefV4")]);

        var result = new InstallDetector(new EmptyUninstallRegistry(), ExpandTestRoot).Detect(program);

        Assert.True(result.IsInstalled);
        Assert.Equal(exe, result.ExecutablePath);
    }

    [Fact]
    public void SkipsFoldersWithUnknownEnvironmentVariables()
    {
        var program = Program(["CrewChiefV4.exe"], [@"%THIS_VARIABLE_DOES_NOT_EXIST_123%\CrewChiefV4"]);

        var result = new InstallDetector(new EmptyUninstallRegistry()).Detect(program);

        Assert.False(result.IsInstalled);
    }

    [Fact]
    public void FindsProgramThroughInstalledProgramsList_InSubfolder()
    {
        // e.g. iRacing installed to a custom drive: InstallLocation = D:\Games\iRacing, exe in ui\
        var exe = _temp.CreateFile("Games", "iRacing", "ui", "iRacingUI.exe");
        var registry = new StaticRegistry(new UninstallEntry("iRacing.com Race Simulation", Path.Combine(_temp.Path, "Games", "iRacing"), null, null));

        var result = new InstallDetector(registry).Detect(Program(["iRacingUI.exe"], uninstallNames: ["iRacing"]));

        Assert.Equal(exe, result.ExecutablePath);
    }

    [Fact]
    public void UsesUninstallStringFolder_WhenInstallLocationIsMissing()
    {
        // Squirrel-style install: %LOCALAPPDATA%\irdashies\Update.exe --uninstall
        var root = Path.Combine(_temp.Path, "irdashies");
        var stub = _temp.CreateFile("irdashies", "irdashies.exe");
        _temp.CreateFile("irdashies", "app-3.0.0", "irdashies.exe");
        var registry = new StaticRegistry(new UninstallEntry("irdashies", null, null, $"\"{Path.Combine(root, "Update.exe")}\" --uninstall"));

        var result = new InstallDetector(registry).Detect(Program(["irdashies.exe"], uninstallNames: ["irdashies"]));

        // The launcher stub in the root wins over the versioned copy.
        Assert.Equal(stub, result.ExecutablePath);
    }

    [Fact]
    public void UsesDisplayIconFolder()
    {
        var exe = _temp.CreateFile("Bloops", "Bloops.exe");
        var registry = new StaticRegistry(new UninstallEntry("Bloops 1.2.3", "", $"{exe},0", null));

        var result = new InstallDetector(registry).Detect(Program(["Bloops.exe"], uninstallNames: ["Bloops"]));

        Assert.Equal(exe, result.ExecutablePath);
    }

    [Fact]
    public void IgnoresUnrelatedInstalledPrograms()
    {
        _temp.CreateFile("Other", "Bloops.exe");
        var registry = new StaticRegistry(new UninstallEntry("Something Else", Path.Combine(_temp.Path, "Other"), null, null));

        var result = new InstallDetector(registry).Detect(Program(["Bloops.exe"], uninstallNames: ["Bloops"]));

        Assert.False(result.IsInstalled);
    }

    [Fact]
    public void MatchesFileNamesCaseInsensitively()
    {
        var exe = _temp.CreateFile("app", "IRACINGUI.EXE");

        var found = InstallDetector.FindExecutable(Path.Combine(_temp.Path, "app"), ["iRacingUI.exe"], 2);

        Assert.Equal(exe, found);
    }

    [Fact]
    public void RespectsMaximumDepth()
    {
        _temp.CreateFile("root", "a", "b", "c", "deep.exe");

        Assert.Null(InstallDetector.FindExecutable(Path.Combine(_temp.Path, "root"), ["deep.exe"], 2));
        Assert.NotNull(InstallDetector.FindExecutable(Path.Combine(_temp.Path, "root"), ["deep.exe"], 3));
    }

    [Fact]
    public void BroadFoldersAreNotCrawled()
    {
        _temp.CreateFile("Programs", "SomeApp", "app.exe");

        Assert.True(InstallDetector.IsTooBroad(Path.Combine(_temp.Path, "Programs")));
        Assert.Null(InstallDetector.FindExecutable(Path.Combine(_temp.Path, "Programs"), ["app.exe"], 2));
        Assert.True(InstallDetector.IsTooBroad(Path.GetPathRoot(_temp.Path)!));
    }

    [Fact]
    public void DetectAll_CoversTheWholeCatalog_AndSurvivesRegistryErrors()
    {
        var results = new InstallDetector(new ThrowingRegistry()).DetectAll();

        Assert.Equal(ProgramCatalog.All.Count, results.Count);
    }

    [Fact]
    public void Catalog_IsConsistent()
    {
        Assert.Equal(ProgramCatalog.All.Count, ProgramCatalog.All.Select(p => p.Id).Distinct().Count());
        Assert.All(ProgramCatalog.All, p =>
        {
            Assert.NotEmpty(p.ExeNames);
            Assert.NotEmpty(p.UninstallNames);
            Assert.StartsWith("https://", p.WebsiteUrl);
        });
        Assert.Equal(ProgramCatalog.All.OrderBy(p => p.Order).Select(p => p.Id), ProgramCatalog.All.Select(p => p.Id));
        Assert.Equal(ProgramCategory.Required, ProgramCatalog.TradingPaints.Category);
        Assert.Equal(ProgramCatalog.IRacingId, ProgramCatalog.All[^1].Id);
        Assert.Equal(ProgramCatalog.MozaPitHouseId, ProgramCatalog.All[0].Id);
    }

    [Fact]
    public void WindowsRegistry_CanBeRead_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var entries = new WindowsUninstallRegistry().GetEntries();
        Assert.NotEmpty(entries);
    }

    private sealed class StaticRegistry(params UninstallEntry[] entries) : IUninstallRegistry
    {
        public IReadOnlyList<UninstallEntry> GetEntries() => entries;
    }

    private sealed class ThrowingRegistry : IUninstallRegistry
    {
        public IReadOnlyList<UninstallEntry> GetEntries() => throw new UnauthorizedAccessException("nope");
    }
}
