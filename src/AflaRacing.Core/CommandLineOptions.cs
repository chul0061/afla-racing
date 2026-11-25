namespace AflaRacing.Core;

/// <summary>What a desktop shortcut asks AFLA Racing to do without opening the main window.</summary>
public enum QuickAction
{
    None,
    Start,
    Stop,
}

/// <summary>AFLA Racing's command line.</summary>
public sealed record CommandLineOptions(QuickAction QuickAction, bool AutoStart, string? DataDirectory, string? SmokeTestOutput)
{
    /// <summary>Start every program in the start list, show a small progress popup and exit.</summary>
    public const string StartArgument = "--start";

    /// <summary>Stop every program, show a small progress popup and exit.</summary>
    public const string StopArgument = "--stop";

    /// <summary>Launched by Windows at sign-in (opens minimized).</summary>
    public const string AutoStartArgument = "--autostart";

    public const string DataDirectoryArgument = "--data-dir";

    public const string SmokeTestArgument = "--smoke-test";

    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        var quickAction = QuickAction.None;
        var autoStart = false;
        string? dataDirectory = null;
        string? smokeTestOutput = null;

        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i].Trim().ToLowerInvariant())
            {
                case StartArgument:
                    quickAction = QuickAction.Start;
                    break;
                case StopArgument:
                    quickAction = QuickAction.Stop;
                    break;
                case AutoStartArgument:
                    autoStart = true;
                    break;
                case DataDirectoryArgument when i + 1 < args.Count:
                    dataDirectory = args[++i];
                    break;
                case SmokeTestArgument when i + 1 < args.Count:
                    smokeTestOutput = args[++i];
                    break;
            }
        }

        return new CommandLineOptions(quickAction, autoStart, dataDirectory, smokeTestOutput);
    }
}
