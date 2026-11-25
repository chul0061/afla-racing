using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using AflaRacing.App.Infrastructure;
using AflaRacing.App.ViewModels;
using AflaRacing.Core;
using AflaRacing.Core.Catalog;
using AflaRacing.Core.Launching;

namespace AflaRacing.App.Views;

public partial class MainWindow : Window
{
    private readonly AppServices _services;
    private readonly ObservableCollection<StatusRowViewModel> _rows = [];
    private readonly DispatcherTimer _statusTimer;
    private bool _busy;
    private bool _refreshing;

    public MainWindow(AppServices services, bool startMinimized = false)
    {
        InitializeComponent();
        _services = services;

        Title = AppInfo.Name;
        TitleText.Text = AppInfo.Name;
        VersionText.Text = App.VersionText;
        StatusList.ItemsSource = _rows;
        if (startMinimized)
        {
            WindowState = WindowState.Minimized;
        }

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _statusTimer.Tick += async (_, _) => await RefreshStatusAsync();
        _services.DetectionUpdated += OnDetectionUpdated;
        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            _statusTimer.Stop();
            _services.DetectionUpdated -= OnDetectionUpdated;
        };
    }

    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        RebuildRows();
        _statusTimer.Start();

        if (App.IsSmokeTest)
        {
            return;
        }

        try
        {
            await _services.DetectAsync();
        }
        catch (Exception ex)
        {
            AppLog.Warn("설치된 프로그램을 찾다가 오류가 났어요.", ex);
        }

        if (!_services.Settings.FirstRunCompleted)
        {
            ShowFirstRunSetup();
        }
    }

    private void OnDetectionUpdated(object? sender, EventArgs e) => UpdateBanner();

    /// <summary>Recreates the status rows after the settings changed.</summary>
    public void RebuildRows()
    {
        _rows.Clear();
        foreach (var item in _services.Settings.StartItems)
        {
            _rows.Add(new StatusRowViewModel(item));
        }

        var enabled = _services.Settings.StartItems.Count(i => i.Enabled);
        ListCountText.Text = _rows.Count == 0 ? "" : $"{_rows.Count}개 중 {enabled}개 사용";
        EmptyState.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateBanner();
        _ = RefreshStatusAsync();
    }

    private void UpdateBanner()
    {
        // Only warn once detection has finished, so the banner doesn't flash on startup.
        var missing = _services.LastDetection is not null && !_services.IsTradingPaintsAvailable();
        TradingPaintsBanner.Visibility = missing ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task RefreshStatusAsync()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            var rows = _rows.ToList();
            var processes = _services.Processes;
            var states = await Task.Run(() =>
            {
                var running = processes.GetRunningProcessNames();
                return rows.Select(r => ItemStatus.Evaluate(r.Item, running)).ToList();
            });

            for (var i = 0; i < rows.Count; i++)
            {
                rows[i].Update(states[i]);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("상태를 새로 고치지 못했어요.", ex);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async void OnStartClick(object sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        var settings = _services.Settings;
        if (!settings.StartItems.Any(i => i.Enabled))
        {
            Shell.Info(this, "시작 목록에 켤 프로그램이 없어요.\n[설정]에서 추가하거나 [자동으로 찾기]를 눌러 보세요.");
            return;
        }

        if (_services.LastDetection is not null && !_services.IsTradingPaintsAvailable() &&
            !Shell.Confirm(this, "Trading Paints가 설치되어 있지 않아요.\n다른 드라이버들의 페인트가 안 보이게 돼요.\n\n그래도 시작할까요?"))
        {
            return;
        }

        using var actionLock = ActionLock.TryAcquire();
        if (actionLock is null)
        {
            Shell.Info(this, "바탕화면 바로가기로 시작이나 종료가 진행 중이에요.\n끝난 뒤에 다시 눌러 주세요.");
            return;
        }

        SetBusy(true, "시작하는 중...");
        try
        {
            var items = settings.StartItems.Select(i => i.Clone()).ToList();
            var progress = new Progress<LaunchProgress>(p =>
                StatusText.Text = $"{p.ItemName} · {p.Message} ({p.Index + 1}/{p.Total})");
            var results = await Task.Run(() => _services.Launcher.StartAllAsync(items, progress));
            var summary = Summaries.ForStart(results);
            StatusText.Text = summary.Headline;

            if (summary.HasProblems)
            {
                Shell.Warning(this, summary.ToMessage());
            }
            else if (settings.MinimizeAfterStart)
            {
                WindowState = WindowState.Minimized;
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("시작하다가 오류가 났어요.", ex);
            StatusText.Text = "시작하다가 오류가 났어요.";
            Shell.Error(this, $"시작하다가 오류가 났어요.\n{ex.Message}");
        }
        finally
        {
            SetBusy(false);
            await RefreshStatusAsync();
        }
    }

    private async void OnStopClick(object sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        var settings = _services.Settings;
        if (settings.ConfirmStopWhileDriving &&
            _services.Launcher.IsAnyRunning(ProgramCatalog.DrivingProcessNames) &&
            !Shell.Confirm(this, "지금 iRacing 주행 중이에요.\n그래도 모두 종료할까요?"))
        {
            return;
        }

        using var actionLock = ActionLock.TryAcquire();
        if (actionLock is null)
        {
            Shell.Info(this, "바탕화면 바로가기로 시작이나 종료가 진행 중이에요.\n끝난 뒤에 다시 눌러 주세요.");
            return;
        }

        SetBusy(true, "종료하는 중...");
        try
        {
            var snapshot = settings.Clone();
            var progress = new Progress<LaunchProgress>(p =>
                StatusText.Text = $"{p.ItemName} · {p.Message} ({p.Index + 1}/{p.Total})");
            var results = await Task.Run(() => _services.Launcher.StopAllAsync(snapshot, progress));
            var summary = Summaries.ForStop(results);
            StatusText.Text = summary.Headline;
            if (summary.HasProblems)
            {
                Shell.Warning(this, summary.ToMessage());
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("종료하다가 오류가 났어요.", ex);
            StatusText.Text = "종료하다가 오류가 났어요.";
            Shell.Error(this, $"종료하다가 오류가 났어요.\n{ex.Message}");
        }
        finally
        {
            SetBusy(false);
            await RefreshStatusAsync();
        }
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        StartButton.IsEnabled = !busy;
        StopButton.IsEnabled = !busy;
        BusyBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (message is not null)
        {
            StatusText.Text = message;
        }
    }

    private void OnInstallTradingPaintsClick(object sender, RoutedEventArgs e) =>
        Shell.OpenUrl(ProgramCatalog.TradingPaints.WebsiteUrl);

    private void OnQuickLinkClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is StatusRowViewModel { QuickLinkUrl: { } url })
        {
            Shell.OpenUrl(url);
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e) => OpenSettings(SettingsTab.StartList);

    private void OnRecommendedClick(object sender, RoutedEventArgs e) => OpenSettings(SettingsTab.Recommended);

    private void OnAutoDetectClick(object sender, RoutedEventArgs e) => ShowFirstRunSetup();

    private void OpenSettings(SettingsTab tab)
    {
        if (_busy)
        {
            return;
        }

        var window = new SettingsWindow(_services, tab) { Owner = this };
        if (window.ShowDialog() != true)
        {
            return;
        }

        _services.Settings = window.Result;
        SaveSettings();
        StartupRegistration.Apply(_services.Settings.RunOnWindowsStartup);
        RebuildRows();
    }

    private void ShowFirstRunSetup()
    {
        var window = new FirstRunWindow(_services) { Owner = this };
        var confirmed = window.ShowDialog() == true;
        if (confirmed)
        {
            var added = StartListBuilder.Merge(_services.Settings.StartItems, window.SelectedResults);
            AppLog.Info($"자동 설정으로 {added}개 추가");
        }

        _services.Settings.FirstRunCompleted = true;
        SaveSettings();
        RebuildRows();

        if (confirmed && window.CreateDesktopShortcuts && CreateDesktopShortcuts(this, _services))
        {
            StatusText.Text = "시작 목록을 만들고, 바탕화면에 시작·종료 바로가기도 만들었어요.";
        }
    }

    /// <summary>Creates the desktop shortcuts; shows an error and returns false when that fails.</summary>
    public static bool CreateDesktopShortcuts(Window owner, AppServices services)
    {
        try
        {
            DesktopShortcuts.Create(services.DataDirectory);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("바탕화면 바로가기를 만들지 못했어요.", ex);
            Shell.Error(owner, $"바탕화면 바로가기를 만들지 못했어요.\n{ex.Message}");
            return false;
        }
    }

    private void SaveSettings()
    {
        try
        {
            _services.Save();
        }
        catch (Exception ex)
        {
            AppLog.Error("설정을 저장하지 못했어요.", ex);
            Shell.Error(this, $"설정을 저장하지 못했어요.\n{ex.Message}");
        }
    }
}
