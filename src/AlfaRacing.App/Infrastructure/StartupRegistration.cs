using AlfaRacing.Core;
using Microsoft.Win32;

namespace AlfaRacing.App.Infrastructure;

/// <summary>"Start with Windows" via HKCU\...\Run (no admin rights needed).</summary>
public static class StartupRegistration
{
    public const string AutoStartArgument = "--autostart";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Adds or removes the Run entry. Re-writing it also fixes the path after the exe was moved.</summary>
    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe))
                {
                    return;
                }

                key.SetValue(AppInfo.Name, $"\"{exe}\" {AutoStartArgument}");
            }
            else
            {
                key.DeleteValue(AppInfo.Name, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("자동 실행 설정을 바꾸지 못했어요.", ex);
        }
    }
}
