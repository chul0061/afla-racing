namespace AlfaRacing.Core.Models;

/// <summary>
/// A program that ALFA Racing starts with the start button and (usually) closes with the stop button.
/// </summary>
public sealed class LaunchItem
{
    public string Id { get; set; } = NewId();

    /// <summary>Name shown in the UI.</summary>
    public string Name { get; set; } = "";

    /// <summary>Executable, shortcut (.lnk) or a URI such as <c>steam://rungameid/266410</c>.</summary>
    public string Path { get; set; } = "";

    /// <summary>Command line arguments passed to the program.</summary>
    public string Arguments { get; set; } = "";

    /// <summary>Seconds to wait after the previously launched program before launching this one.</summary>
    public int DelaySeconds { get; set; }

    /// <summary>When false the item stays in the list but is not started.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>When true the stop button also closes this program.</summary>
    public bool StopOnExit { get; set; } = true;

    /// <summary>
    /// Process names (as shown in Task Manager's Details tab, without ".exe") used to detect and stop the program.
    /// A trailing <c>*</c> matches by prefix. When empty the name is derived from <see cref="Path"/>.
    /// </summary>
    public List<string> ProcessNames { get; set; } = [];

    /// <summary>Id of the <see cref="Catalog.KnownProgram"/> this item was created from, if any.</summary>
    public string? CatalogId { get; set; }

    public LaunchItem Clone() => new()
    {
        Id = Id,
        Name = Name,
        Path = Path,
        Arguments = Arguments,
        DelaySeconds = DelaySeconds,
        Enabled = Enabled,
        StopOnExit = StopOnExit,
        ProcessNames = [.. ProcessNames],
        CatalogId = CatalogId,
    };

    public static string NewId() => Guid.NewGuid().ToString("N");
}
