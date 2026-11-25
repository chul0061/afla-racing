using AflaRacing.Core.Models;
using AflaRacing.Core.Processes;

namespace AflaRacing.Core.Launching;

public static class LaunchItemExtensions
{
    /// <summary>The configured process names, or the one derived from the exe path when none are configured.</summary>
    public static IReadOnlyList<ProcessNamePattern> GetProcessPatterns(this LaunchItem item)
    {
        var configured = ProcessNamePattern.ParseMany(item.ProcessNames);
        if (configured.Count > 0)
        {
            return configured;
        }

        var derived = PathHelpers.DeriveProcessName(PathHelpers.Normalize(item.Path));
        return derived is not null && ProcessNamePattern.TryParse(derived, out var pattern, out _)
            ? [pattern]
            : [];
    }

    public static string DisplayName(this LaunchItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Name))
        {
            return item.Name.Trim();
        }

        var path = item.Path?.Trim() ?? "";
        return path.Length == 0 ? "(이름 없음)" : PathHelpers.GetFileName(path);
    }
}
