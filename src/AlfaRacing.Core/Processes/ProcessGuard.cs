namespace AlfaRacing.Core.Processes;

/// <summary>
/// Processes ALFA Racing must never close, whatever the user typed (Windows itself and ALFA Racing).
/// </summary>
public static class ProcessGuard
{
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "Secure System", "Memory Compression",
        "smss", "csrss", "wininit", "winlogon", "services", "lsass", "lsaiso",
        "svchost", "fontdrvhost", "dwm", "explorer", "sihost", "ctfmon", "taskhostw",
        "RuntimeBroker", "StartMenuExperienceHost", "ShellExperienceHost", "ShellHost",
        "SearchHost", "SearchApp", "SearchIndexer", "TextInputHost", "LockApp",
        "conhost", "spoolsv", "audiodg", "dllhost", "WmiPrvSE", "WUDFHost",
        "MsMpEng", "NisSrv", "SecurityHealthService", "SecurityHealthSystray", "SgrmBroker",
        "Taskmgr", "ApplicationFrameHost", "SystemSettings", "UserOOBEBroker",
        AppInfo.ProcessName,
    };

    public static bool IsProtected(string? processName) =>
        string.IsNullOrWhiteSpace(processName) || Protected.Contains(processName);
}
