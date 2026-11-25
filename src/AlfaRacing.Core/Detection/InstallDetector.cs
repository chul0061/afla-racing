using AlfaRacing.Core.Catalog;

namespace AlfaRacing.Core.Detection;

/// <summary>An entry from Windows' installed programs list (the registry "Uninstall" keys).</summary>
public sealed record UninstallEntry(string DisplayName, string? InstallLocation, string? DisplayIcon, string? UninstallString);

public interface IUninstallRegistry
{
    IReadOnlyList<UninstallEntry> GetEntries();
}

/// <summary>Used on non-Windows systems and in tests.</summary>
public sealed class EmptyUninstallRegistry : IUninstallRegistry
{
    public IReadOnlyList<UninstallEntry> GetEntries() => [];
}

public sealed record DetectionResult(KnownProgram Program, string? ExecutablePath)
{
    public bool IsInstalled => ExecutablePath is not null;
}

/// <summary>
/// Finds installed programs: first in their usual folders, then via the installed programs list
/// (which also covers custom install folders and Steam installs).
/// </summary>
public sealed class InstallDetector
{
    /// <summary>How many folder levels below a search root are searched.</summary>
    public const int MaxSearchDepth = 2;

    private readonly IUninstallRegistry _registry;
    private readonly Func<string, string> _expand;

    public InstallDetector(IUninstallRegistry registry, Func<string, string>? expandEnvironmentVariables = null)
    {
        _registry = registry;
        _expand = expandEnvironmentVariables ?? Environment.ExpandEnvironmentVariables;
    }

    public IReadOnlyList<DetectionResult> DetectAll(IEnumerable<KnownProgram>? programs = null)
    {
        var entries = LoadEntries();
        return (programs ?? ProgramCatalog.All).Select(p => Detect(p, entries)).ToList();
    }

    /// <param name="entries">Installed programs list; read from the registry when null.</param>
    public DetectionResult Detect(KnownProgram program, IReadOnlyList<UninstallEntry>? entries = null)
    {
        if (program.ExeNames.Count == 0)
        {
            return new DetectionResult(program, null);
        }

        foreach (var root in GetSearchRoots(program, entries ?? LoadEntries()))
        {
            var found = FindExecutable(root, program.ExeNames, MaxSearchDepth);
            if (found is not null)
            {
                return new DetectionResult(program, found);
            }
        }

        return new DetectionResult(program, null);
    }

    private IReadOnlyList<UninstallEntry> LoadEntries()
    {
        try
        {
            return _registry.GetEntries();
        }
        catch (Exception ex)
        {
            AppLog.Warn("설치 목록(레지스트리)을 읽지 못했어요.", ex);
            return [];
        }
    }

    private IEnumerable<string> GetSearchRoots(KnownProgram program, IReadOnlyList<UninstallEntry> entries)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in program.SearchDirectories)
        {
            var expanded = _expand(dir);
            // Skip entries whose variables don't exist on this machine (left unexpanded).
            if (expanded.Contains('%') || !seen.Add(expanded))
            {
                continue;
            }

            yield return expanded;
        }

        foreach (var entry in entries)
        {
            if (!program.UninstallNames.Any(n => entry.DisplayName.Contains(n, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var candidates = new[]
            {
                entry.InstallLocation?.Trim().Trim('"').Trim(),
                PathHelpers.GetParentDirectory(PathHelpers.ExtractExecutablePath(entry.DisplayIcon)),
                PathHelpers.GetParentDirectory(PathHelpers.ExtractExecutablePath(entry.UninstallString)),
            };

            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    /// <summary>
    /// Breadth-first search for any of <paramref name="exeNames"/> (case-insensitive) up to <paramref name="maxDepth"/>
    /// levels below <paramref name="root"/>. Shallower matches win, so a launcher stub in the install root is
    /// preferred over versioned copies such as "app-1.2.3\app.exe".
    /// </summary>
    public static string? FindExecutable(string root, IReadOnlyList<string> exeNames, int maxDepth)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return null;
            }
        }
        catch (Exception)
        {
            return null;
        }

        // Never crawl huge folders such as "C:\" or "C:\Program Files" – only look directly inside them.
        var depthLimit = IsTooBroad(root) ? 0 : maxDepth;
        var options = new EnumerationOptions
        {
            MatchCasing = MatchCasing.CaseInsensitive,
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
        };

        var level = new List<string> { root };
        for (var depth = 0; depth <= depthLimit && level.Count > 0; depth++)
        {
            foreach (var dir in level)
            {
                foreach (var exeName in exeNames)
                {
                    try
                    {
                        var match = Directory.EnumerateFiles(dir, exeName, options).FirstOrDefault();
                        if (match is not null)
                        {
                            return match;
                        }
                    }
                    catch (Exception)
                    {
                        // Unreadable folder: ignore.
                    }
                }
            }

            if (depth == depthLimit)
            {
                break;
            }

            var next = new List<string>();
            foreach (var dir in level)
            {
                try
                {
                    next.AddRange(Directory.EnumerateDirectories(dir, "*", options).Order(StringComparer.OrdinalIgnoreCase));
                }
                catch (Exception)
                {
                    // Unreadable folder: ignore.
                }
            }

            level = next;
        }

        return null;
    }

    private static readonly Environment.SpecialFolder[] BroadFolders =
    [
        Environment.SpecialFolder.ProgramFiles,
        Environment.SpecialFolder.ProgramFilesX86,
        Environment.SpecialFolder.LocalApplicationData,
        Environment.SpecialFolder.ApplicationData,
        Environment.SpecialFolder.CommonApplicationData,
        Environment.SpecialFolder.UserProfile,
        Environment.SpecialFolder.Windows,
        Environment.SpecialFolder.System,
        Environment.SpecialFolder.SystemX86,
        Environment.SpecialFolder.Desktop,
    ];

    internal static bool IsTooBroad(string directory)
    {
        string full;
        try
        {
            full = Path.GetFullPath(directory).TrimEnd('\\', '/');
        }
        catch (Exception)
        {
            return true;
        }

        if (full.Length <= 3 || Path.GetPathRoot(full)?.TrimEnd('\\', '/') == full)
        {
            return true;
        }

        foreach (var folder in BroadFolders)
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(path) && string.Equals(path.TrimEnd('\\', '/'), full, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // e.g. "...\AppData\Local\Programs" or "...\Steam\steamapps\common" hold many apps.
        var name = Path.GetFileName(full);
        return name.Equals("Programs", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("common", StringComparison.OrdinalIgnoreCase);
    }
}
