using System.IO;
using AlfaRacing.Core;
using AlfaRacing.Core.Catalog;
using AlfaRacing.Core.Detection;
using AlfaRacing.Core.Launching;
using AlfaRacing.Core.Models;
using AlfaRacing.Core.Processes;
using AlfaRacing.Core.Settings;

namespace AlfaRacing.App.Infrastructure;

/// <summary>Everything the windows share: settings, launcher, detection.</summary>
public sealed class AppServices
{
    public AppServices(string dataDirectory, IProcessService? processes = null, IUninstallRegistry? registry = null)
    {
        DataDirectory = dataDirectory;
        Store = new SettingsStore(dataDirectory);
        Processes = processes ?? new SystemProcessService();
        Launcher = new Launcher(Processes);
        Detector = new InstallDetector(registry ?? new WindowsUninstallRegistry());
    }

    public string DataDirectory { get; }

    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    public SettingsStore Store { get; }

    public IProcessService Processes { get; }

    public Launcher Launcher { get; }

    public InstallDetector Detector { get; }

    public AppSettings Settings { get; set; } = new();

    /// <summary>Most recent auto-detection result (null until the first detection finished).</summary>
    public IReadOnlyList<DetectionResult>? LastDetection { get; private set; }

    public event EventHandler? DetectionUpdated;

    public void Save() => Store.Save(Settings);

    public async Task<IReadOnlyList<DetectionResult>> DetectAsync()
    {
        var results = await Task.Run(() => Detector.DetectAll()).ConfigureAwait(true);
        SetDetection(results);
        return results;
    }

    /// <summary>Used by the smoke test to show fixed data.</summary>
    public void SetDetection(IReadOnlyList<DetectionResult> results)
    {
        LastDetection = results;
        foreach (var result in results)
        {
            AppLog.Info($"검색: {result.Program.Name} → {result.ExecutablePath ?? "없음"}");
        }

        DetectionUpdated?.Invoke(this, EventArgs.Empty);
    }

    public DetectionResult? FindDetection(string catalogId) =>
        LastDetection?.FirstOrDefault(r => r.Program.Id == catalogId);

    public bool IsTradingPaintsAvailable() =>
        ItemStatus.IsTradingPaintsAvailable(Settings, FindDetection(ProgramCatalog.TradingPaintsId));
}
