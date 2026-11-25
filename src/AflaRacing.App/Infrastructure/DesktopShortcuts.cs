using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows;
using AflaRacing.Core;

namespace AflaRacing.App.Infrastructure;

/// <summary>
/// "AFLA Racing 시작" / "AFLA Racing 종료" shortcuts on the desktop. They run the exe with --start / --stop,
/// which starts or stops everything without opening the main window.
/// </summary>
public static class DesktopShortcuts
{
    public sealed record Definition(QuickAction Action, string FileName, string Argument, string Description, string IconResource);

    public static IReadOnlyList<Definition> All { get; } =
    [
        new(QuickAction.Start, $"{AppInfo.Name} 시작.lnk", CommandLineOptions.StartArgument,
            "레이싱 프로그램을 순서대로 모두 켜요", "shortcut-start.ico"),
        new(QuickAction.Stop, $"{AppInfo.Name} 종료.lnk", CommandLineOptions.StopArgument,
            "레이싱 프로그램을 모두 꺼요", "shortcut-stop.ico"),
    ];

    public static string DesktopDirectory => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static bool AllExist() => All.All(d => File.Exists(Path.Combine(DesktopDirectory, d.FileName)));

    /// <summary>Creates (or overwrites) both shortcuts on the desktop and returns their paths.</summary>
    /// <param name="targetDirectory">Folder for the shortcuts; the desktop when null (the smoke test uses a temp folder).</param>
    public static IReadOnlyList<string> Create(string dataDirectory, string? targetDirectory = null)
    {
        var exe = CurrentExecutable();
        var folder = targetDirectory ?? DesktopDirectory;
        Directory.CreateDirectory(folder);
        var created = new List<string>();
        foreach (var definition in All)
        {
            var path = Path.Combine(folder, definition.FileName);
            Write(path, exe, definition, dataDirectory);
            created.Add(path);
        }

        AppLog.Info($"바탕화면 바로가기 생성: {string.Join(", ", created)}");
        return created;
    }

    /// <summary>
    /// Points shortcuts that already exist at the current exe, e.g. after the exe was moved or a new version
    /// was downloaded somewhere else. Never throws.
    /// </summary>
    public static void RefreshExisting(string dataDirectory)
    {
        try
        {
            var exe = CurrentExecutable();
            foreach (var definition in All)
            {
                var path = Path.Combine(DesktopDirectory, definition.FileName);
                if (!File.Exists(path))
                {
                    continue;
                }

                var target = ReadTargetPath(path);
                if (!string.Equals(target, exe, StringComparison.OrdinalIgnoreCase))
                {
                    Write(path, exe, definition, dataDirectory);
                    AppLog.Info($"바로가기 경로 갱신: {path} → {exe}");
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("바탕화면 바로가기를 갱신하지 못했어요.", ex);
        }
    }

    private static string CurrentExecutable() =>
        Environment.ProcessPath ?? throw new InvalidOperationException("실행 파일 경로를 알 수 없어요.");

    private static void Write(string shortcutPath, string exe, Definition definition, string dataDirectory)
    {
        var icon = ExtractIcon(definition.IconResource, Path.Combine(dataDirectory, "icons"));
        var link = (IShellLinkW)new ShellLink();
        try
        {
            link.SetPath(exe);
            link.SetArguments(definition.Argument);
            link.SetWorkingDirectory(Path.GetDirectoryName(exe) ?? "");
            link.SetDescription(definition.Description);
            link.SetIconLocation(icon, 0);
            ((IPersistFile)link).Save(shortcutPath, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    /// <summary>The program a .lnk file points to.</summary>
    public static string? ReadTargetPath(string shortcutPath)
    {
        var link = (IShellLinkW)new ShellLink();
        try
        {
            ((IPersistFile)link).Load(shortcutPath, 0);
            var buffer = new StringBuilder(1024);
            link.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 0);
            return buffer.ToString();
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    /// <summary>Shortcut icons must be real files, so the embedded .ico is copied to the data folder.</summary>
    private static string ExtractIcon(string resourceName, string iconDirectory)
    {
        Directory.CreateDirectory(iconDirectory);
        var target = Path.Combine(iconDirectory, resourceName);
        var resource = Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/{resourceName}", UriKind.Absolute))
                       ?? throw new FileNotFoundException("아이콘 리소스를 찾을 수 없어요.", resourceName);
        using (var source = resource.Stream)
        using (var file = File.Create(target))
        {
            source.CopyTo(file);
        }

        return target;
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, IntPtr findData, uint flags);

        void GetIDList(out IntPtr idList);

        void SetIDList(IntPtr idList);

        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);

        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);

        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int maxPath);

        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);

        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int maxPath);

        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);

        void GetHotkey(out short hotkey);

        void SetHotkey(short hotkey);

        void GetShowCmd(out int showCmd);

        void SetShowCmd(int showCmd);

        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int maxIconPath, out int iconIndex);

        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);

        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);

        void Resolve(IntPtr hwnd, uint flags);

        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }
}
