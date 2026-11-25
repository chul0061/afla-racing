using AlfaRacing.Core.Catalog;
using AlfaRacing.Core.Detection;
using AlfaRacing.Core.Launching;
using AlfaRacing.Core.Models;
using AlfaRacing.Core.Settings;

namespace AlfaRacing.Core.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void MissingFile_GivesDefaults()
    {
        var result = new SettingsStore(_temp.Path).Load();

        Assert.True(result.IsNew);
        Assert.Null(result.Warning);
        Assert.Empty(result.Settings.StartItems);
        Assert.False(result.Settings.FirstRunCompleted);
        Assert.True(result.Settings.ConfirmStopWhileDriving);
        Assert.Equal(AppSettings.DefaultForceKillAfterSeconds, result.Settings.ForceKillAfterSeconds);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new SettingsStore(Path.Combine(_temp.Path, "nested"));
        var settings = new AppSettings
        {
            FirstRunCompleted = true,
            MinimizeAfterStart = true,
            ForceKillAfterSeconds = 7,
            ExtraStopProcesses = ["SimHubWPF"],
            StartItems =
            [
                new LaunchItem
                {
                    Name = "모자 핏하우스",
                    Path = @"C:\Program Files (x86)\MOZA Pit House\MOZA Pit House.exe",
                    DelaySeconds = 2,
                    StopOnExit = false,
                    ProcessNames = ["MOZA Pit House"],
                    CatalogId = ProgramCatalog.MozaPitHouseId,
                },
            ],
        };

        store.Save(settings);
        var json = File.ReadAllText(store.FilePath);
        var loaded = store.Load();

        Assert.Contains("모자 핏하우스", json); // Korean stays readable in the file.
        Assert.False(loaded.IsNew);
        var item = Assert.Single(loaded.Settings.StartItems);
        Assert.Equal("모자 핏하우스", item.Name);
        Assert.Equal(@"C:\Program Files (x86)\MOZA Pit House\MOZA Pit House.exe", item.Path);
        Assert.Equal(2, item.DelaySeconds);
        Assert.False(item.StopOnExit);
        Assert.Equal(ProgramCatalog.MozaPitHouseId, item.CatalogId);
        Assert.Equal(7, loaded.Settings.ForceKillAfterSeconds);
        Assert.True(loaded.Settings.MinimizeAfterStart);
        Assert.Equal(["SimHubWPF"], loaded.Settings.ExtraStopProcesses);
        Assert.False(File.Exists(store.FilePath + ".tmp"));
    }

    [Fact]
    public void CorruptFile_IsBackedUp_AndDefaultsAreUsed()
    {
        var store = new SettingsStore(_temp.Path);
        File.WriteAllText(store.FilePath, "{ this is not json");

        var result = store.Load();

        Assert.True(result.IsNew);
        Assert.NotNull(result.Warning);
        Assert.Single(Directory.GetFiles(_temp.Path, "settings.broken-*.json"));
    }

    [Fact]
    public void HandEditedValues_AreRepaired()
    {
        var store = new SettingsStore(_temp.Path);
        File.WriteAllText(store.FilePath, """
        {
          // comments and trailing commas are fine
          "forceKillAfterSeconds": 0,
          "extraStopProcesses": ["a", " A ", ""],
          "startItems": [
            { "id": "same", "name": "One", "path": "C:\\one.exe", "delaySeconds": -4 },
            { "id": "same", "name": "Two", "path": "C:\\two.exe", "delaySeconds": 99999, "processNames": ["x", "", "X"] },
          ],
        }
        """);

        var settings = store.Load().Settings;

        Assert.Equal(AppSettings.DefaultForceKillAfterSeconds, settings.ForceKillAfterSeconds);
        Assert.Equal(["a"], settings.ExtraStopProcesses);
        Assert.Equal(0, settings.StartItems[0].DelaySeconds);
        Assert.Equal(AppSettings.MaxDelaySeconds, settings.StartItems[1].DelaySeconds);
        Assert.NotEqual(settings.StartItems[0].Id, settings.StartItems[1].Id);
        Assert.Equal(["x"], settings.StartItems[1].ProcessNames);
        Assert.True(settings.StartItems[0].Enabled);
        Assert.True(settings.StartItems[0].StopOnExit);
    }
}

public class StartListBuilderTests
{
    private static DetectionResult Found(string id, string path) => new(ProgramCatalog.Find(id)!, path);

    private static DetectionResult Missing(string id) => new(ProgramCatalog.Find(id)!, null);

    [Fact]
    public void Build_UsesRecommendedOrder_AndOnlyInstalledPrograms()
    {
        var items = StartListBuilder.Build(
        [
            Found(ProgramCatalog.IRacingId, @"C:\Program Files (x86)\iRacing\ui\iRacingUI.exe"),
            Found(ProgramCatalog.CrewChiefId, @"C:\CC\CrewChiefV4.exe"),
            Missing(ProgramCatalog.BloopsId),
            Found(ProgramCatalog.MozaPitHouseId, @"C:\MOZA\MOZA Pit House.exe"),
            Found(ProgramCatalog.TradingPaintsId, @"C:\TP\Trading Paints.exe"),
        ]);

        Assert.Equal(["MOZA Pit House", "Trading Paints", "CrewChief", "iRacing"], items.Select(i => i.Name));
        Assert.All(items, i => Assert.True(i.Enabled && i.StopOnExit));
    }

    [Fact]
    public void CreateItem_SetsProcessNames()
    {
        var moza = StartListBuilder.CreateItem(ProgramCatalog.Find(ProgramCatalog.MozaPitHouseId)!, @"C:\MOZA\MOZA Pit House.exe");
        var iracing = StartListBuilder.CreateItem(ProgramCatalog.Find(ProgramCatalog.IRacingId)!, @"C:\iRacing\ui\iRacingUI.exe");

        Assert.Equal(["MOZA Pit House"], moza.ProcessNames);
        Assert.Equal(["iRacingUI", "iRacingSim*"], iracing.ProcessNames);
        Assert.Equal(ProgramCatalog.IRacingId, iracing.CatalogId);
    }

    [Fact]
    public void InsertInRecommendedOrder_PutsNewProgramsBeforeIRacing_AndKeepsCustomItems()
    {
        var items = StartListBuilder.Build(
        [
            Found(ProgramCatalog.MozaPitHouseId, @"C:\MOZA\MOZA Pit House.exe"),
            Found(ProgramCatalog.IRacingId, @"C:\iRacing\ui\iRacingUI.exe"),
        ]);
        items.Insert(1, new LaunchItem { Name = "SimHub", Path = @"C:\SimHub\SimHubWPF.exe" });

        StartListBuilder.InsertInRecommendedOrder(items, StartListBuilder.CreateItem(ProgramCatalog.Find(ProgramCatalog.CrewChiefId)!, @"C:\CC\CrewChiefV4.exe"));
        StartListBuilder.InsertInRecommendedOrder(items, new LaunchItem { Name = "Custom" });

        Assert.Equal(["MOZA Pit House", "SimHub", "CrewChief", "iRacing", "Custom"], items.Select(i => i.Name));
    }

    [Fact]
    public void Merge_AddsOnlyMissingPrograms()
    {
        var items = StartListBuilder.Build([Found(ProgramCatalog.MozaPitHouseId, @"C:\MOZA\MOZA Pit House.exe")]);

        var added = StartListBuilder.Merge(items,
        [
            Found(ProgramCatalog.MozaPitHouseId, @"C:\Other\MOZA Pit House.exe"),
            Found(ProgramCatalog.TradingPaintsId, @"C:\TP\Trading Paints.exe"),
        ]);

        Assert.Equal(1, added);
        Assert.Equal(["MOZA Pit House", "Trading Paints"], items.Select(i => i.Name));
        Assert.Equal(@"C:\MOZA\MOZA Pit House.exe", items[0].Path);
    }
}

public class ItemStatusTests
{
    private static readonly string[] Running = ["iRacingSim64DX11", "CrewChiefV4"];

    [Fact]
    public void Evaluate_States()
    {
        static bool Exists(string path) => !path.Contains("missing");

        Assert.Equal(ItemRunState.Running, ItemStatus.Evaluate(new LaunchItem { Path = @"C:\x\CrewChiefV4.exe" }, Running, Exists));
        Assert.Equal(ItemRunState.Running, ItemStatus.Evaluate(new LaunchItem { Path = "steam://rungameid/266410", ProcessNames = ["iRacingSim*"] }, Running, Exists));
        Assert.Equal(ItemRunState.Stopped, ItemStatus.Evaluate(new LaunchItem { Path = @"C:\x\Bloops.exe" }, Running, Exists));
        Assert.Equal(ItemRunState.Disabled, ItemStatus.Evaluate(new LaunchItem { Path = @"C:\x\Bloops.exe", Enabled = false }, Running, Exists));
        Assert.Equal(ItemRunState.Missing, ItemStatus.Evaluate(new LaunchItem { Path = @"C:\missing\Bloops.exe" }, Running, Exists));
        Assert.Equal(ItemRunState.Missing, ItemStatus.Evaluate(new LaunchItem { Path = "" }, Running, Exists));
        Assert.Equal(ItemRunState.Unknown, ItemStatus.Evaluate(new LaunchItem { Path = @"C:\x\Thing.lnk" }, Running, Exists));
    }

    [Fact]
    public void TradingPaints_IsAvailable_WhenDetected_OrInStartList()
    {
        var tp = ProgramCatalog.TradingPaints;
        var empty = new AppSettings();

        Assert.True(ItemStatus.IsTradingPaintsAvailable(empty, new DetectionResult(tp, @"C:\TP\Trading Paints.exe")));
        Assert.False(ItemStatus.IsTradingPaintsAvailable(empty, new DetectionResult(tp, null)));
        Assert.False(ItemStatus.IsTradingPaintsAvailable(empty, null));

        var custom = new AppSettings { StartItems = [new LaunchItem { Name = "TP", Path = @"D:\Apps\TP\Trading Paints.exe" }] };
        Assert.True(ItemStatus.IsTradingPaintsAvailable(custom, null, _ => true));
        Assert.False(ItemStatus.IsTradingPaintsAvailable(custom, null, _ => false));
    }
}
