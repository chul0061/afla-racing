using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AlfaRacing.App.Views;
using AlfaRacing.Core;
using AlfaRacing.Core.Catalog;
using AlfaRacing.Core.Detection;
using AlfaRacing.Core.Launching;
using AlfaRacing.Core.Models;
using AlfaRacing.Core.Processes;

namespace AlfaRacing.App.Infrastructure;

/// <summary>
/// "--smoke-test &lt;folder&gt;": opens every window with sample data, saves a PNG of each and exits with 0 (all good)
/// or 1 (something threw). Used by CI to catch XAML/runtime errors and to preview the UI.
/// </summary>
internal static class SmokeTest
{
    private static readonly List<string> Errors = [];

    public static void RecordError(Exception exception) => Errors.Add(exception.ToString());

    public static int Run(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var dataDirectory = Path.Combine(outputDirectory, "data");
        var fakeRoot = Path.Combine(outputDirectory, "fake-programs");
        var captured = new List<string>();

        try
        {
            var paths = CreateFakePrograms(fakeRoot);
            var running = new PreviewProcessService(["MOZA Pit House", "Trading Paints", "CrewChiefV4"]);

            // Normal state: everything set up, some programs running.
            var services = new AppServices(dataDirectory, running, new EmptyUninstallRegistry());
            services.Settings = SampleSettings(paths);
            services.SetDetection(SampleDetection(paths, tradingPaintsInstalled: true));

            Capture(new MainWindow(services), outputDirectory, "01-main.png", captured);
            Capture(new SettingsWindow(services, SettingsTab.StartList, SampleRunningApps()), outputDirectory, "02-settings-start-list.png", captured);
            Capture(new SettingsWindow(services, SettingsTab.Stop), outputDirectory, "03-settings-stop.png", captured);
            Capture(new SettingsWindow(services, SettingsTab.Recommended), outputDirectory, "04-settings-recommended.png", captured);
            Capture(new SettingsWindow(services, SettingsTab.General), outputDirectory, "05-settings-general.png", captured);
            Capture(new ItemEditorWindow(services.Settings.StartItems[2], isNew: false), outputDirectory, "06-item-editor.png", captured);
            Capture(new ProcessPickerWindow(SampleRunningApps()), outputDirectory, "07-process-picker.png", captured);

            // Trading Paints missing → warning banner and first-run warning.
            var missing = new AppServices(dataDirectory, running, new EmptyUninstallRegistry());
            missing.Settings = SampleSettings(paths);
            missing.Settings.StartItems.RemoveAll(i => i.CatalogId == ProgramCatalog.TradingPaintsId);
            missing.SetDetection(SampleDetection(paths, tradingPaintsInstalled: false));
            Capture(new MainWindow(missing), outputDirectory, "08-main-trading-paints-missing.png", captured);
            Capture(new FirstRunWindow(missing), outputDirectory, "09-first-run.png", captured);

            // Brand-new install: empty start list.
            var empty = new AppServices(dataDirectory, running, new EmptyUninstallRegistry());
            empty.SetDetection(SampleDetection(paths, tradingPaintsInstalled: true));
            Capture(new MainWindow(empty), outputDirectory, "10-main-empty.png", captured);
        }
        catch (Exception ex)
        {
            Errors.Add(ex.ToString());
        }

        var report = new StringBuilder();
        report.AppendLine(Errors.Count == 0 ? "OK" : "FAILED");
        report.AppendLine($"captured: {string.Join(", ", captured)}");
        foreach (var error in Errors)
        {
            report.AppendLine().AppendLine(error);
        }

        File.WriteAllText(Path.Combine(outputDirectory, "smoke-result.txt"), report.ToString());
        return Errors.Count == 0 ? 0 : 1;
    }

    private static void Capture(Window window, string outputDirectory, string fileName, List<string> captured)
    {
        try
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = 24;
            window.Top = 24;
            window.ShowActivated = false;
            window.Show();
            Pump(TimeSpan.FromMilliseconds(900));
            window.UpdateLayout();
            SavePng(window, Path.Combine(outputDirectory, fileName));
            captured.Add(fileName);
        }
        catch (Exception ex)
        {
            Errors.Add($"{fileName}: {ex}");
        }
        finally
        {
            try
            {
                window.Close();
            }
            catch (Exception)
            {
                // Ignore.
            }
        }
    }

    private static void SavePng(Window window, string path)
    {
        if (VisualTreeHelper.GetChildrenCount(window) == 0 || VisualTreeHelper.GetChild(window, 0) is not FrameworkElement root)
        {
            throw new InvalidOperationException("창 내용이 없어요.");
        }

        const double scale = 2.0;
        var width = Math.Max(1, (int)Math.Ceiling(root.ActualWidth * scale));
        var height = Math.Max(1, (int)Math.Ceiling(root.ActualHeight * scale));
        var bitmap = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);

        // Paint the window background first so transparent areas don't come out black.
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var bounds = new Rect(0, 0, root.ActualWidth, root.ActualHeight);
            context.DrawRectangle(window.Background ?? Brushes.Black, null, bounds);
            context.DrawRectangle(new VisualBrush(root), null, bounds);
        }

        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void Pump(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static Dictionary<string, string> CreateFakePrograms(string root)
    {
        var result = new Dictionary<string, string>();
        foreach (var program in ProgramCatalog.All)
        {
            var path = Path.Combine(root, program.Name, program.ExeNames[0]);
            if (program.Id != ProgramCatalog.BloopsId)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "");
            }

            result[program.Id] = path;
        }

        return result;
    }

    private static AppSettings SampleSettings(Dictionary<string, string> paths)
    {
        var settings = new AppSettings
        {
            FirstRunCompleted = true,
            ExtraStopProcesses = ["SimHubWPF"],
        };

        foreach (var program in ProgramCatalog.All)
        {
            settings.StartItems.Add(StartListBuilder.CreateItem(program, paths[program.Id]));
        }

        settings.StartItems.First(i => i.CatalogId == ProgramCatalog.TradingPaintsId).StopOnExit = false;
        return settings;
    }

    private static List<DetectionResult> SampleDetection(Dictionary<string, string> paths, bool tradingPaintsInstalled) =>
        ProgramCatalog.All
            .Select(p => new DetectionResult(
                p,
                p.Id == ProgramCatalog.BloopsId || (p.Id == ProgramCatalog.TradingPaintsId && !tradingPaintsInstalled)
                    ? null
                    : paths[p.Id]))
            .ToList();

    private static List<RunningApp> SampleRunningApps() =>
    [
        new("CrewChiefV4", "CrewChiefV4", @"C:\Program Files (x86)\Britton IT Ltd\CrewChiefV4\CrewChiefV4.exe", true),
        new("MOZA Pit House", "MOZA Pit House", @"C:\Program Files (x86)\MOZA Pit House\MOZA Pit House.exe", true),
        new("SimHub", "SimHubWPF", @"C:\Program Files (x86)\SimHub\SimHubWPF.exe", true),
        new("Discord", "Discord", @"C:\Users\driver\AppData\Local\Discord\app-1.0.9200\Discord.exe", false),
    ];

    private sealed class PreviewProcessService(IReadOnlyList<string> running) : IProcessService
    {
        public IReadOnlyList<string> GetRunningProcessNames() => running;

        public IReadOnlyList<IRunningProcess> FindProcesses(IReadOnlyList<ProcessNamePattern> patterns) => [];

        public void Start(string fileName, string arguments, string? workingDirectory)
        {
        }
    }
}
