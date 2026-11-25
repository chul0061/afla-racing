using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using AflaRacing.Core;
using AflaRacing.Core.Processes;

namespace AflaRacing.App.Infrastructure;

public sealed record RunningApp(string DisplayName, string ProcessName, string ExecutablePath, bool HasWindow);

/// <summary>Lists running programs with their exe paths (for "add from running programs").</summary>
public static class RunningApps
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    public static IReadOnlyList<RunningApp> GetAll()
    {
        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var ownId = Environment.ProcessId;
        var byPath = new Dictionary<string, RunningApp>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == ownId || process.SessionId == 0)
                {
                    continue; // ourselves and Windows services
                }

                var name = process.ProcessName;
                if (ProcessGuard.IsProtected(name))
                {
                    continue;
                }

                var path = GetExecutablePath(process.Id);
                if (string.IsNullOrEmpty(path) ||
                    (!string.IsNullOrEmpty(windowsDir) && path.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var hasWindow = process.MainWindowHandle != IntPtr.Zero;
                if (byPath.TryGetValue(path, out var existing))
                {
                    if (hasWindow && !existing.HasWindow)
                    {
                        byPath[path] = existing with { HasWindow = true };
                    }

                    continue;
                }

                byPath[path] = new RunningApp(GetDisplayName(path, name), name, path, hasWindow);
            }
            catch (Exception)
            {
                // Exited or not accessible: skip.
            }
            finally
            {
                process.Dispose();
            }
        }

        return byPath.Values
            .OrderByDescending(a => a.HasWindow)
            .ThenBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Friendly name from the exe's version info ("CrewChiefV4"), falling back to the file name.</summary>
    public static string GetDisplayName(string exePath, string? fallback = null)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            var candidate = !string.IsNullOrWhiteSpace(info.FileDescription) ? info.FileDescription : info.ProductName;
            if (!string.IsNullOrWhiteSpace(candidate) && candidate.Trim().Length <= 60)
            {
                return candidate.Trim();
            }
        }
        catch (Exception)
        {
            // No version info.
        }

        return fallback ?? Path.GetFileNameWithoutExtension(exePath);
    }

    private static string? GetExecutablePath(int processId)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString(0, size) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder exeName, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
