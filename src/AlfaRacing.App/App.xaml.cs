using System.IO;
using System.Windows;
using System.Windows.Threading;
using AlfaRacing.App.Infrastructure;
using AlfaRacing.App.Views;
using AlfaRacing.Core;

namespace AlfaRacing.App;

public partial class App : Application
{
    private SingleInstance? _singleInstance;

    /// <summary>True while running with --smoke-test (renders every window to PNG, then exits).</summary>
    public static bool IsSmokeTest { get; private set; }

    public static string VersionText { get; } = GetVersionText();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var options = CommandLineOptions.Parse(e.Args);
        IsSmokeTest = options.SmokeTestOutput is not null;
        var dataDirectory = options.DataDirectory
                            ?? (IsSmokeTest ? Path.Combine(options.SmokeTestOutput!, "data") : AppInfo.DefaultDataDirectory);

        AppLog.Initialize(Path.Combine(dataDirectory, "logs"));
        AppLog.Info($"{AppInfo.Name} {VersionText} 시작");
        RegisterExceptionHandlers();
        ApplyDarkTheme();

        if (IsSmokeTest)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(SmokeTest.Run(options.SmokeTestOutput!));
            return;
        }

        if (options.QuickAction != QuickAction.None)
        {
            RunQuickAction(options.QuickAction, dataDirectory);
            return;
        }

        _singleInstance = SingleInstance.TryAcquire();
        if (_singleInstance is null)
        {
            AppLog.Info("이미 실행 중이라 기존 창을 앞으로 가져왔어요.");
            Shutdown();
            return;
        }

        var services = new AppServices(dataDirectory);
        var loaded = services.Store.Load();
        services.Settings = loaded.Settings;
        if (services.Settings.RunOnWindowsStartup)
        {
            StartupRegistration.Apply(true); // keeps the path right if the exe was moved
        }

        DesktopShortcuts.RefreshExisting(dataDirectory); // same for the desktop shortcuts

        var window = new MainWindow(services, options.AutoStart);
        MainWindow = window;
        _singleInstance.ActivationRequested += () => Dispatcher.InvokeAsync(window.BringToFront);
        window.Show();

        if (loaded.Warning is not null)
        {
            Shell.Warning(window, loaded.Warning);
        }
    }

    /// <summary>Desktop shortcut mode: start or stop everything with a small popup, then exit.</summary>
    private void RunQuickAction(QuickAction action, string dataDirectory)
    {
        AppLog.Info($"바로가기 실행: {action}");
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var services = new AppServices(dataDirectory);
        var loaded = services.Store.Load();
        services.Settings = loaded.Settings;

        var window = new QuickActionWindow(services, action);
        MainWindow = window;
        window.Closed += (_, _) => Shutdown(window.ExitCode);
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        AppLog.Info($"{AppInfo.Name} 종료 (코드 {e.ApplicationExitCode})");
        base.OnExit(e);
    }

    private void ApplyDarkTheme()
    {
        try
        {
            // Windows 11 style (Fluent) controls in dark mode.
            ThemeMode = ThemeMode.Dark;
        }
        catch (Exception ex)
        {
            AppLog.Warn("Fluent 테마를 적용하지 못해 기본 테마로 계속해요.", ex);
        }
    }

    private void RegisterExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("치명적인 오류", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("백그라운드 작업 오류", args.Exception);
            args.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        AppLog.Error("처리되지 않은 오류", args.Exception);
        args.Handled = true;

        if (IsSmokeTest)
        {
            SmokeTest.RecordError(args.Exception);
            return;
        }

        Shell.Error(MainWindow, $"예상하지 못한 오류가 났어요.\n{args.Exception.Message}\n\n로그 파일: {AppLog.FilePath}");
    }

    private static string GetVersionText()
    {
        var version = typeof(App).Assembly.GetName().Version;
        return version is null ? "" : $"v{version.Major}.{version.Minor}.{version.Build}";
    }
}
