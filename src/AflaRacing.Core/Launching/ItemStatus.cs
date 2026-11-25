using AflaRacing.Core.Catalog;
using AflaRacing.Core.Detection;
using AflaRacing.Core.Models;
using AflaRacing.Core.Processes;

namespace AflaRacing.Core.Launching;

public enum ItemRunState
{
    Running,
    Stopped,
    Missing,
    Disabled,
    Unknown,
}

public static class ItemStatus
{
    public static ItemRunState Evaluate(LaunchItem item, IReadOnlyCollection<string> runningProcessNames, Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;
        var patterns = item.GetProcessPatterns();
        if (patterns.Count > 0 && ProcessNamePattern.AnyMatch(patterns, runningProcessNames))
        {
            return ItemRunState.Running;
        }

        if (!item.Enabled)
        {
            return ItemRunState.Disabled;
        }

        var path = PathHelpers.Normalize(item.Path);
        if (path.Length == 0 || (!PathHelpers.IsUri(path) && !fileExists(path)))
        {
            return ItemRunState.Missing;
        }

        return patterns.Count == 0 ? ItemRunState.Unknown : ItemRunState.Stopped;
    }

    public static string Label(ItemRunState state) => state switch
    {
        ItemRunState.Running => "실행 중",
        ItemRunState.Stopped => "꺼짐",
        ItemRunState.Missing => "파일 없음",
        ItemRunState.Disabled => "사용 안 함",
        _ => "상태 확인 불가",
    };

    /// <summary>
    /// True when Trading Paints can be used: it was detected, or the start list contains a working Trading Paints entry.
    /// </summary>
    public static bool IsTradingPaintsAvailable(AppSettings settings, DetectionResult? detection, Func<string, bool>? fileExists = null)
    {
        if (detection?.IsInstalled == true)
        {
            return true;
        }

        fileExists ??= File.Exists;
        var exeNames = ProgramCatalog.TradingPaints.ExeNames;
        foreach (var item in settings.StartItems)
        {
            var path = PathHelpers.Normalize(item.Path);
            if (path.Length == 0 || PathHelpers.IsUri(path))
            {
                continue;
            }

            var isTradingPaints =
                string.Equals(item.CatalogId, ProgramCatalog.TradingPaintsId, StringComparison.OrdinalIgnoreCase) ||
                exeNames.Any(n => PathHelpers.GetFileName(path).Equals(n, StringComparison.OrdinalIgnoreCase));
            if (isTradingPaints && fileExists(path))
            {
                return true;
            }
        }

        return false;
    }
}
