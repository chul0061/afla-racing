using System.Diagnostics;
using System.IO;
using System.Windows;
using AflaRacing.Core;

namespace AflaRacing.App.Infrastructure;

/// <summary>Opens web pages and folders, and shows message boxes.</summary>
public static class Shell
{
    public static void OpenUrl(string url)
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Warn($"링크를 열지 못했어요: {url}", ex);
            Error(null, $"링크를 열지 못했어요.\n{url}");
        }
    }

    public static void OpenFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            using var _ = Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Warn($"폴더를 열지 못했어요: {folder}", ex);
            Error(null, $"폴더를 열지 못했어요.\n{folder}");
        }
    }

    public static void Info(Window? owner, string message) =>
        Show(owner, message, MessageBoxButton.OK, MessageBoxImage.Information);

    public static void Warning(Window? owner, string message) =>
        Show(owner, message, MessageBoxButton.OK, MessageBoxImage.Warning);

    public static void Error(Window? owner, string message) =>
        Show(owner, message, MessageBoxButton.OK, MessageBoxImage.Error);

    public static bool Confirm(Window? owner, string message) =>
        Show(owner, message, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    private static MessageBoxResult Show(Window? owner, string message, MessageBoxButton buttons, MessageBoxImage image)
    {
        if (App.IsSmokeTest)
        {
            return MessageBoxResult.Yes;
        }

        return owner is { IsLoaded: true }
            ? MessageBox.Show(owner, message, AppInfo.Name, buttons, image)
            : MessageBox.Show(message, AppInfo.Name, buttons, image);
    }
}
