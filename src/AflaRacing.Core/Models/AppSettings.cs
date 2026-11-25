namespace AflaRacing.Core.Models;

/// <summary>Everything AFLA Racing stores in settings.json.</summary>
public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;
    public const int DefaultForceKillAfterSeconds = 3;
    public const int MaxDelaySeconds = 600;
    public const int MaxForceKillAfterSeconds = 60;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Programs to start, in order. The stop button closes them in reverse order.</summary>
    public List<LaunchItem> StartItems { get; set; } = [];

    /// <summary>Extra process names the stop button closes (programs that are not in the start list).</summary>
    public List<string> ExtraStopProcesses { get; set; } = [];

    /// <summary>How long to wait for a program to close by itself before it is force-killed.</summary>
    public int ForceKillAfterSeconds { get; set; } = DefaultForceKillAfterSeconds;

    /// <summary>Ask before stopping while the iRacing simulator is running.</summary>
    public bool ConfirmStopWhileDriving { get; set; } = true;

    /// <summary>Minimize the AFLA Racing window after everything started successfully.</summary>
    public bool MinimizeAfterStart { get; set; }

    /// <summary>Start AFLA Racing when Windows starts.</summary>
    public bool RunOnWindowsStartup { get; set; }

    /// <summary>False until the first-run setup (auto detection) was completed or skipped.</summary>
    public bool FirstRunCompleted { get; set; }

    public AppSettings Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        StartItems = StartItems.Select(i => i.Clone()).ToList(),
        ExtraStopProcesses = [.. ExtraStopProcesses],
        ForceKillAfterSeconds = ForceKillAfterSeconds,
        ConfirmStopWhileDriving = ConfirmStopWhileDriving,
        MinimizeAfterStart = MinimizeAfterStart,
        RunOnWindowsStartup = RunOnWindowsStartup,
        FirstRunCompleted = FirstRunCompleted,
    };

    /// <summary>Repairs values that may be missing or out of range (e.g. from a hand-edited file).</summary>
    public void Normalize()
    {
        StartItems ??= [];
        StartItems.RemoveAll(i => i is null);
        ExtraStopProcesses ??= [];
        ExtraStopProcesses = ExtraStopProcesses
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ForceKillAfterSeconds < 1 || ForceKillAfterSeconds > MaxForceKillAfterSeconds)
        {
            ForceKillAfterSeconds = DefaultForceKillAfterSeconds;
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in StartItems)
        {
            item.Name ??= "";
            item.Path ??= "";
            item.Arguments ??= "";
            item.ProcessNames ??= [];
            item.ProcessNames = item.ProcessNames
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            item.DelaySeconds = Math.Clamp(item.DelaySeconds, 0, MaxDelaySeconds);
            if (string.IsNullOrWhiteSpace(item.Id) || !seenIds.Add(item.Id))
            {
                item.Id = LaunchItem.NewId();
                seenIds.Add(item.Id);
            }
        }

        SchemaVersion = CurrentSchemaVersion;
    }
}
