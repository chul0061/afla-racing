namespace AflaRacing.Core.Catalog;

public enum ProgramCategory
{
    /// <summary>Must be installed (Trading Paints).</summary>
    Required,

    /// <summary>The sim itself.</summary>
    Game,

    /// <summary>Wheel base / pedal software.</summary>
    Hardware,

    /// <summary>Optional tools we recommend.</summary>
    Recommended,
}

/// <summary>A shortcut button shown next to the program in the main window (e.g. "pick a livery").</summary>
public sealed record QuickLink(string Label, string Url, string ToolTip);

/// <summary>A program AFLA Racing knows how to find, recommend and add to the start list.</summary>
public sealed record KnownProgram
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required ProgramCategory Category { get; init; }

    public required string Description { get; init; }

    /// <summary>Download / install page.</summary>
    public required string WebsiteUrl { get; init; }

    /// <summary>Main website, used once the program is installed. Falls back to <see cref="WebsiteUrl"/>.</summary>
    public string? HomepageUrl { get; init; }

    /// <summary>Optional button shown on the program's row in the main window.</summary>
    public QuickLink? QuickLink { get; init; }

    /// <summary>Executable file names to look for, e.g. "CrewChiefV4.exe".</summary>
    public IReadOnlyList<string> ExeNames { get; init; } = [];

    /// <summary>Folders to search (may contain %ENVIRONMENT% variables).</summary>
    public IReadOnlyList<string> SearchDirectories { get; init; } = [];

    /// <summary>Text that appears in the program's "Apps &amp; features" (uninstall) display name.</summary>
    public IReadOnlyList<string> UninstallNames { get; init; } = [];

    /// <summary>Process names for detection/stopping. Empty = derived from the detected exe.</summary>
    public IReadOnlyList<string> ProcessNames { get; init; } = [];

    /// <summary>Launch URI to use when the exe cannot be found (e.g. Steam).</summary>
    public string? AlternativeLaunchUri { get; init; }

    public int DefaultDelaySeconds { get; init; }

    /// <summary>Position in the recommended start order (lower starts first).</summary>
    public int Order { get; init; }

    public string CategoryLabel => Category switch
    {
        ProgramCategory.Required => "필수",
        ProgramCategory.Game => "게임",
        ProgramCategory.Hardware => "장비",
        _ => "추천",
    };
}
