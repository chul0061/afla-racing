using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AflaRacing.App.Infrastructure;
using AflaRacing.Core;
using AflaRacing.Core.Catalog;
using AflaRacing.Core.Launching;

namespace AflaRacing.App.Views;

/// <summary>
/// Small popup in the bottom-right corner used by the desktop shortcuts (--start / --stop):
/// shows progress, then the result, and closes by itself when everything went fine.
/// </summary>
public partial class QuickActionWindow : Window
{
    private static readonly TimeSpan AutoCloseDelay = TimeSpan.FromSeconds(3);

    private readonly AppServices _services;
    private readonly QuickAction _action;
    private readonly DispatcherTimer _closeTimer = new() { Interval = AutoCloseDelay };

    /// <param name="autoRun">False only for the smoke test, which fills the window by hand.</param>
    public QuickActionWindow(AppServices services, QuickAction action, bool autoRun = true)
    {
        InitializeComponent();
        _services = services;
        _action = action;

        Title = AppInfo.Name;
        TitleText.Text = action == QuickAction.Start ? $"{AppInfo.Name} · 시작" : $"{AppInfo.Name} · 종료";
        OpenAppButton.Content = $"{AppInfo.Name} 열기";

        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            Close();
        };
        Closed += (_, _) => _closeTimer.Stop();
        SizeChanged += (_, _) => PlaceBottomRight();
        Loaded += async (_, _) =>
        {
            PlaceBottomRight();
            if (autoRun)
            {
                await RunAsync();
            }
        };
    }

    /// <summary>0 when everything went fine; used as the process exit code.</summary>
    public int ExitCode { get; private set; } = 1;

    public void ShowProgress(string text)
    {
        DetailText.Text = text;
        Progress.Visibility = Visibility.Visible;
    }

    public void ShowResult(RunSummary summary)
    {
        Progress.Visibility = Visibility.Collapsed;
        DetailText.Text = summary.Headline;

        if (summary.HasProblems)
        {
            ExitCode = 1;
            ShowDetails(string.Join("\n", summary.Problems) + (summary.Tip is null ? "" : "\n\n" + summary.Tip));
            return;
        }

        ExitCode = 0;
        _closeTimer.Start();
    }

    private void ShowMessage(string headline, string details)
    {
        Progress.Visibility = Visibility.Collapsed;
        DetailText.Text = headline;
        ShowDetails(details);
    }

    private void ShowDetails(string details)
    {
        ProblemsText.Text = details;
        ProblemsText.Visibility = Visibility.Visible;
        ButtonsPanel.Visibility = Visibility.Visible;
    }

    private async Task RunAsync()
    {
        using var actionLock = ActionLock.TryAcquire();
        if (actionLock is null)
        {
            ShowMessage("이미 시작이나 종료가 진행 중이에요.", "지금 하던 작업이 끝난 뒤에 다시 눌러 주세요.");
            return;
        }

        try
        {
            if (_action == QuickAction.Start)
            {
                await StartAsync();
            }
            else
            {
                await StopAsync();
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("바로가기 실행 중 오류", ex);
            ShowMessage("오류가 났어요.", $"{ex.Message}\n\n로그: {AppLog.FilePath}");
        }
    }

    private async Task StartAsync()
    {
        var settings = _services.Settings;
        if (!settings.StartItems.Any(i => i.Enabled))
        {
            ShowMessage("시작 목록이 비어 있어요.", $"{AppInfo.Name}을(를) 열어서 켤 프로그램을 추가해 주세요.");
            return;
        }

        ShowProgress("확인하는 중...");
        if (!await IsTradingPaintsAvailableAsync() &&
            !Shell.Confirm(this, "Trading Paints가 설치되어 있지 않아요.\n다른 드라이버들의 페인트가 안 보이게 돼요.\n\n그래도 시작할까요?"))
        {
            Close();
            return;
        }

        ShowProgress("시작하는 중...");
        var items = settings.StartItems.Select(i => i.Clone()).ToList();
        var progress = new Progress<LaunchProgress>(p => ShowProgress($"{p.ItemName} · {p.Message} ({p.Index + 1}/{p.Total})"));
        var results = await Task.Run(() => _services.Launcher.StartAllAsync(items, progress));
        ShowResult(Summaries.ForStart(results));
    }

    private async Task StopAsync()
    {
        var settings = _services.Settings;
        if (settings.ConfirmStopWhileDriving &&
            _services.Launcher.IsAnyRunning(ProgramCatalog.DrivingProcessNames) &&
            !Shell.Confirm(this, "지금 iRacing 주행 중이에요.\n그래도 모두 종료할까요?"))
        {
            Close();
            return;
        }

        ShowProgress("종료하는 중...");
        var snapshot = settings.Clone();
        var progress = new Progress<LaunchProgress>(p => ShowProgress($"{p.ItemName} · {p.Message} ({p.Index + 1}/{p.Total})"));
        var results = await Task.Run(() => _services.Launcher.StopAllAsync(snapshot, progress));
        ShowResult(Summaries.ForStop(results));
    }

    private async Task<bool> IsTradingPaintsAvailableAsync()
    {
        if (ItemStatus.IsTradingPaintsAvailable(_services.Settings, null))
        {
            return true;
        }

        var detection = await Task.Run(() => _services.Detector.Detect(ProgramCatalog.TradingPaints));
        return detection.IsInstalled;
    }

    private void PlaceBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 16;
        Top = area.Bottom - ActualHeight - 16;
    }

    private void OnDragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnOpenAppClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Environment.ProcessPath is { } exe)
            {
                using var _ = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("메인 창을 열지 못했어요.", ex);
        }

        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
