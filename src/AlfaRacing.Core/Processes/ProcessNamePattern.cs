namespace AlfaRacing.Core.Processes;

/// <summary>
/// A process name to match ("CrewChiefV4") or a prefix pattern ending in '*' ("iRacingSim*").
/// Matching is case-insensitive, like Windows.
/// </summary>
public readonly record struct ProcessNamePattern
{
    public const int MinimumPrefixLength = 3;

    private ProcessNamePattern(string text, bool isPrefix)
    {
        Text = text;
        IsPrefix = isPrefix;
    }

    /// <summary>The name without ".exe" (and without the trailing '*' for prefix patterns).</summary>
    public string Text { get; }

    public bool IsPrefix { get; }

    public override string ToString() => IsPrefix ? Text + "*" : Text;

    public bool Matches(string? processName)
    {
        if (string.IsNullOrEmpty(processName) || string.IsNullOrEmpty(Text))
        {
            return false;
        }

        return IsPrefix
            ? processName.StartsWith(Text, StringComparison.OrdinalIgnoreCase)
            : processName.Equals(Text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses user input. Accepts "Name", "Name.exe", a full path to an exe, or "Prefix*".
    /// </summary>
    public static bool TryParse(string? raw, out ProcessNamePattern pattern, out string? error)
    {
        pattern = default;
        error = null;

        var text = (raw ?? "").Trim().Trim('"').Trim();
        if (text.Length == 0)
        {
            error = "프로세스 이름이 비어 있어요.";
            return false;
        }

        text = PathHelpers.GetFileName(text).Trim();
        if (text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^4].TrimEnd();
        }

        var isPrefix = text.EndsWith('*');
        if (isPrefix)
        {
            text = text[..^1].TrimEnd();
        }

        if (text.IndexOfAny(['*', '?']) >= 0)
        {
            error = $"'{raw}': '*'는 이름 끝에만 쓸 수 있어요.";
            return false;
        }

        if (text.Length == 0)
        {
            error = $"'{raw}': 이름이 필요해요.";
            return false;
        }

        if (isPrefix && text.Length < MinimumPrefixLength)
        {
            error = $"'{raw}': '*' 앞에 최소 {MinimumPrefixLength}글자가 필요해요.";
            return false;
        }

        pattern = new ProcessNamePattern(text, isPrefix);
        return true;
    }

    /// <summary>Parses every valid entry and silently skips invalid ones.</summary>
    public static IReadOnlyList<ProcessNamePattern> ParseMany(IEnumerable<string>? raw)
    {
        var result = new List<ProcessNamePattern>();
        if (raw is null)
        {
            return result;
        }

        foreach (var entry in raw)
        {
            if (TryParse(entry, out var pattern, out _) && !result.Contains(pattern))
            {
                result.Add(pattern);
            }
        }

        return result;
    }

    public static bool AnyMatch(IReadOnlyList<ProcessNamePattern> patterns, IEnumerable<string> processNames)
    {
        if (patterns.Count == 0)
        {
            return false;
        }

        foreach (var name in processNames)
        {
            foreach (var pattern in patterns)
            {
                if (pattern.Matches(name))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Record structs compare strings ordinally; make equality case-insensitive like Windows.
    public bool Equals(ProcessNamePattern other) =>
        IsPrefix == other.IsPrefix && string.Equals(Text, other.Text, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        HashCode.Combine(IsPrefix, Text is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Text));
}
