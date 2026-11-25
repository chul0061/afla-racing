using AflaRacing.Core.Catalog;
using AflaRacing.Core.Detection;
using AflaRacing.Core.Models;

namespace AflaRacing.Core.Launching;

/// <summary>Creates start-list items from catalog programs and keeps them in the recommended order.</summary>
public static class StartListBuilder
{
    public static LaunchItem CreateItem(KnownProgram program, string path)
    {
        var processNames = program.ProcessNames.Count > 0
            ? program.ProcessNames.ToList()
            : PathHelpers.DeriveProcessName(path) is { } derived ? [derived] : [];

        return new LaunchItem
        {
            Name = program.Name,
            Path = path,
            DelaySeconds = program.DefaultDelaySeconds,
            CatalogId = program.Id,
            ProcessNames = processNames,
        };
    }

    /// <summary>
    /// Item for an exe the user picked. Recognizes catalog programs by file name so they get the right
    /// name, order and process names.
    /// </summary>
    public static LaunchItem CreateFromExecutable(string path, string? processName = null, string? displayName = null)
    {
        var fileName = PathHelpers.GetFileName(path);
        var known = ProgramCatalog.All.FirstOrDefault(p =>
            p.ExeNames.Any(n => n.Equals(fileName, StringComparison.OrdinalIgnoreCase)));
        if (known is not null)
        {
            return CreateItem(known, path);
        }

        var derived = processName ?? PathHelpers.DeriveProcessName(path);
        return new LaunchItem
        {
            Name = string.IsNullOrWhiteSpace(displayName) ? (PathHelpers.DeriveProcessName(path) ?? fileName) : displayName.Trim(),
            Path = path,
            ProcessNames = derived is null ? [] : [derived],
        };
    }

    /// <summary>Start list for the first-run setup: every selected, installed program in the recommended order.</summary>
    public static List<LaunchItem> Build(IEnumerable<DetectionResult> selected) =>
        selected
            .Where(r => r.IsInstalled)
            .OrderBy(r => r.Program.Order)
            .Select(r => CreateItem(r.Program, r.ExecutablePath!))
            .ToList();

    public static bool Contains(IEnumerable<LaunchItem> items, string catalogId) =>
        items.Any(i => string.Equals(i.CatalogId, catalogId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Inserts a catalog item before the first catalog item that should start after it (e.g. before iRacing).
    /// Items without a catalog entry keep their positions; if nothing should start later, the item is appended.
    /// </summary>
    public static void InsertInRecommendedOrder(List<LaunchItem> items, LaunchItem item)
    {
        var order = ProgramCatalog.Find(item.CatalogId)?.Order;
        if (order is null)
        {
            items.Add(item);
            return;
        }

        for (var i = 0; i < items.Count; i++)
        {
            var existingOrder = ProgramCatalog.Find(items[i].CatalogId)?.Order;
            if (existingOrder is not null && existingOrder > order)
            {
                items.Insert(i, item);
                return;
            }
        }

        items.Add(item);
    }

    /// <summary>
    /// Adds detected programs that are not in the list yet (used when re-running auto detection).
    /// Returns how many were added.
    /// </summary>
    public static int Merge(List<LaunchItem> items, IEnumerable<DetectionResult> selected)
    {
        var added = 0;
        foreach (var result in selected.Where(r => r.IsInstalled).OrderBy(r => r.Program.Order))
        {
            if (Contains(items, result.Program.Id))
            {
                continue;
            }

            InsertInRecommendedOrder(items, CreateItem(result.Program, result.ExecutablePath!));
            added++;
        }

        return added;
    }
}
