namespace AflaRacing.Core;

/// <summary>Product naming in one place so the app can be renamed easily.</summary>
public static class AppInfo
{
    /// <summary>Name shown in the UI.</summary>
    public const string Name = "AFLA Racing";

    /// <summary>Folder under %APPDATA% that holds settings and logs.</summary>
    public const string DataFolderName = "AFLA Racing";

    /// <summary>Process name of AFLA Racing itself (AssemblyName of the app project).</summary>
    public const string ProcessName = "AFLARacing";

    public static string DefaultDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DataFolderName);
}
