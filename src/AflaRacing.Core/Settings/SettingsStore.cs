using System.Text.Encodings.Web;
using System.Text.Json;
using AflaRacing.Core.Models;

namespace AflaRacing.Core.Settings;

public sealed record SettingsLoadResult(AppSettings Settings, bool IsNew, string? Warning);

/// <summary>Loads and saves settings.json. A broken file is kept as a backup instead of being lost.</summary>
public sealed class SettingsStore
{
    public const string FileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        // Keep Korean text and paths readable in the file.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public SettingsStore(string directory)
    {
        Directory = directory;
        FilePath = Path.Combine(directory, FileName);
    }

    public string Directory { get; }

    public string FilePath { get; }

    public SettingsLoadResult Load()
    {
        if (!File.Exists(FilePath))
        {
            return new SettingsLoadResult(new AppSettings(), IsNew: true, Warning: null);
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                           ?? throw new JsonException("설정 파일이 비어 있어요.");
            settings.Normalize();
            return new SettingsLoadResult(settings, IsNew: false, Warning: null);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            var backup = Path.Combine(Directory, $"settings.broken-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            try
            {
                File.Copy(FilePath, backup, overwrite: true);
            }
            catch (Exception copyEx)
            {
                AppLog.Warn("깨진 설정 파일을 백업하지 못했어요.", copyEx);
            }

            AppLog.Error("설정 파일을 읽지 못해 기본값으로 시작해요.", ex);
            return new SettingsLoadResult(
                new AppSettings(),
                IsNew: true,
                Warning: $"설정 파일이 손상돼서 처음 상태로 시작해요.\n이전 파일은 여기 백업했어요:\n{backup}");
        }
    }

    /// <summary>Writes atomically (temp file + move) so a crash never leaves half a file.</summary>
    public void Save(AppSettings settings)
    {
        System.IO.Directory.CreateDirectory(Directory);
        settings.Normalize();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, FilePath, overwrite: true);
    }
}
