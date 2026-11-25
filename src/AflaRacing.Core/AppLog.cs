using System.Text;

namespace AflaRacing.Core;

/// <summary>
/// Tiny file logger (logs\afla-racing.log, rotated at 1 MB). Logging never throws.
/// </summary>
public static class AppLog
{
    private const long MaxFileBytes = 1024 * 1024;
    private static readonly Lock Gate = new();
    private static string? _filePath;

    public static string? FilePath => _filePath;

    public static void Initialize(string logDirectory)
    {
        try
        {
            Directory.CreateDirectory(logDirectory);
            lock (Gate)
            {
                _filePath = Path.Combine(logDirectory, "afla-racing.log");
            }
        }
        catch (Exception)
        {
            _filePath = null;
        }
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message, Exception? exception = null) => Write("WARN", message, exception);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        var path = _filePath;
        if (path is null)
        {
            return;
        }

        var line = new StringBuilder()
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append(" [").Append(level).Append("] ")
            .Append(message);
        if (exception is not null)
        {
            line.AppendLine().Append(exception);
        }

        line.AppendLine();

        try
        {
            lock (Gate)
            {
                var info = new FileInfo(path);
                if (info.Exists && info.Length > MaxFileBytes)
                {
                    File.Move(path, Path.ChangeExtension(path, ".old.log"), overwrite: true);
                }

                File.AppendAllText(path, line.ToString(), Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // Logging must never break the app.
        }
    }
}
