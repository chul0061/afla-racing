using System.Runtime.Versioning;
using Microsoft.Win32;

namespace AlfaRacing.Core.Detection;

/// <summary>Reads Windows' installed programs list (machine-wide 64/32-bit and per-user).</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsUninstallRegistry : IUninstallRegistry
{
    private const string UninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public IReadOnlyList<UninstallEntry> GetEntries()
    {
        var result = new List<UninstallEntry>();
        var sources = new (RegistryHive Hive, RegistryView View)[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser, RegistryView.Default),
        };

        foreach (var (hive, view) in sources)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = baseKey.OpenSubKey(UninstallKeyPath);
                if (uninstall is null)
                {
                    continue;
                }

                foreach (var subKeyName in uninstall.GetSubKeyNames())
                {
                    try
                    {
                        using var app = uninstall.OpenSubKey(subKeyName);
                        if (app?.GetValue("DisplayName") is not string displayName || string.IsNullOrWhiteSpace(displayName))
                        {
                            continue;
                        }

                        result.Add(new UninstallEntry(
                            displayName.Trim(),
                            app.GetValue("InstallLocation") as string,
                            app.GetValue("DisplayIcon") as string,
                            app.GetValue("UninstallString") as string));
                    }
                    catch (Exception)
                    {
                        // A broken or protected entry: skip it.
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn($"설치 목록을 읽지 못했어요: {hive}/{view}", ex);
            }
        }

        return result;
    }
}
