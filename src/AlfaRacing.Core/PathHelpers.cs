using System.Text.RegularExpressions;

namespace AlfaRacing.Core;

/// <summary>
/// Path utilities that understand Windows paths on every OS (so they can be unit-tested on Linux).
/// </summary>
public static partial class PathHelpers
{
    private static readonly char[] Separators = ['\\', '/'];

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.\-]+:")]
    private static partial Regex UriSchemeRegex();

    /// <summary>True for launch targets such as <c>steam://rungameid/266410</c> (but not <c>C:\...</c>).</summary>
    public static bool IsUri(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return UriSchemeRegex().IsMatch(path.Trim());
    }

    /// <summary>Trims whitespace and surrounding quotes and expands %ENVIRONMENT% variables.</summary>
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        var trimmed = path.Trim().Trim('"').Trim();
        return IsUri(trimmed) ? trimmed : Environment.ExpandEnvironmentVariables(trimmed);
    }

    /// <summary>File name part of a path, accepting both '\' and '/' separators.</summary>
    public static string GetFileName(string path)
    {
        var index = path.LastIndexOfAny(Separators);
        return index < 0 ? path : path[(index + 1)..];
    }

    /// <summary>Parent directory of a path, accepting both '\' and '/' separators. Null when there is none.</summary>
    public static string? GetParentDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var trimmed = path.TrimEnd(Separators);
        var index = trimmed.LastIndexOfAny(Separators);
        if (index <= 0)
        {
            return null;
        }

        var parent = trimmed[..index];
        // Keep "C:\" instead of "C:" so the result is a usable directory.
        if (parent.Length == 2 && parent[1] == ':')
        {
            parent += trimmed[index];
        }

        return parent;
    }

    /// <summary>
    /// Process name Windows will use for an executable path ("C:\x\CrewChiefV4.exe" → "CrewChiefV4").
    /// Null for shortcuts, scripts and URIs, whose process name cannot be derived from the path.
    /// </summary>
    public static string? DeriveProcessName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsUri(path))
        {
            return null;
        }

        var fileName = GetFileName(path.Trim().Trim('"'));
        if (!fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || fileName.Length <= 4)
        {
            return null;
        }

        return fileName[..^4];
    }

    /// <summary>
    /// Extracts the executable (or icon) path from registry values such as
    /// <c>"C:\App\app.exe" --uninstall</c>, <c>C:\App\Update.exe --uninstall</c> or <c>C:\App\app.exe,0</c>.
    /// Returns null for msiexec-based entries and empty values.
    /// </summary>
    public static string? ExtractExecutablePath(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        var text = commandLine.Trim();
        string candidate;
        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            candidate = end > 1 ? text[1..end] : text.Trim('"');
        }
        else
        {
            var exeIndex = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIndex >= 0)
            {
                candidate = text[..(exeIndex + 4)];
            }
            else
            {
                var comma = text.LastIndexOf(',');
                candidate = comma > 0 ? text[..comma] : text;
            }
        }

        candidate = candidate.Trim();
        // DisplayIcon values look like "C:\App\app.exe,0".
        var iconIndex = candidate.LastIndexOf(',');
        if (iconIndex > 0 && int.TryParse(candidate[(iconIndex + 1)..].Trim(), out _))
        {
            candidate = candidate[..iconIndex].Trim();
        }

        if (candidate.Length == 0)
        {
            return null;
        }

        var fileName = GetFileName(candidate);
        if (fileName.Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("msiexec", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return candidate;
    }
}
