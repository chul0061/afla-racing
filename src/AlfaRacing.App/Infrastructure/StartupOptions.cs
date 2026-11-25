namespace AlfaRacing.App.Infrastructure;

/// <summary>Command line options.</summary>
public sealed record StartupOptions(bool AutoStart, string? DataDirectory, string? SmokeTestOutput)
{
    public static StartupOptions Parse(IReadOnlyList<string> args)
    {
        var autoStart = false;
        string? dataDirectory = null;
        string? smokeTestOutput = null;

        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case StartupRegistration.AutoStartArgument:
                    autoStart = true;
                    break;
                case "--data-dir" when i + 1 < args.Count:
                    dataDirectory = args[++i];
                    break;
                case "--smoke-test" when i + 1 < args.Count:
                    smokeTestOutput = args[++i];
                    break;
            }
        }

        return new StartupOptions(autoStart, dataDirectory, smokeTestOutput);
    }
}
