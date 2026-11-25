using System.Collections.ObjectModel;
using System.Windows;
using AflaRacing.App.Infrastructure;
using AflaRacing.App.ViewModels;
using AflaRacing.Core;
using AflaRacing.Core.Catalog;
using AflaRacing.Core.Detection;

namespace AflaRacing.App.Views;

/// <summary>Finds installed programs and lets the user pick which ones go into the start list.</summary>
public partial class FirstRunWindow : Window
{
    private readonly AppServices _services;
    private readonly ObservableCollection<DetectionRowViewModel> _rows = [];

    /// <param name="offerShortcuts">Show the "create desktop shortcuts" check box (first run only).</param>
    public FirstRunWindow(AppServices services, bool offerShortcuts = true)
    {
        InitializeComponent();
        _services = services;
        Title = $"{AppInfo.Name} 자동 설정";
        WelcomeText.Text = $"{AppInfo.Name}에 오신 걸 환영해요";
        ResultList.ItemsSource = _rows;
        ShortcutCheck.Visibility = offerShortcuts ? Visibility.Visible : Visibility.Collapsed;

        Loaded += async (_, _) =>
        {
            if (_services.LastDetection is { } results)
            {
                ShowResults(results);
            }
            else
            {
                await ScanAsync();
            }
        };
    }

    /// <summary>True when the user wants the desktop start/stop shortcuts.</summary>
    public bool CreateDesktopShortcuts => ShortcutCheck.Visibility == Visibility.Visible && ShortcutCheck.IsChecked == true;

    /// <summary>Checked, installed programs (valid after the window returned true).</summary>
    public IReadOnlyList<DetectionResult> SelectedResults =>
        _rows.Where(r => r.IsChecked && r.IsInstalled).Select(r => r.Result).ToList();

    private async void OnRescanClick(object sender, RoutedEventArgs e) => await ScanAsync();

    private async Task ScanAsync()
    {
        RescanButton.IsEnabled = false;
        ConfirmButton.IsEnabled = false;
        SearchStatusText.Text = "설치된 프로그램을 찾는 중...";
        try
        {
            ShowResults(await _services.DetectAsync());
        }
        catch (Exception ex)
        {
            AppLog.Warn("자동 설정 검색 실패", ex);
            SearchStatusText.Text = $"프로그램을 찾다가 오류가 났어요: {ex.Message}";
        }
        finally
        {
            RescanButton.IsEnabled = true;
        }
    }

    private void ShowResults(IReadOnlyList<DetectionResult> results)
    {
        _rows.Clear();
        foreach (var result in results)
        {
            _rows.Add(new DetectionRowViewModel(result));
        }

        var found = results.Count(r => r.IsInstalled);
        SearchStatusText.Text = found > 0
            ? $"{results.Count}개 중 {found}개를 찾았어요. 시작 목록에 넣을 프로그램을 골라 주세요. 위에서부터 이 순서대로 켜져요."
            : "설치된 프로그램을 찾지 못했어요. [다운로드]로 설치하거나, 나중에 설정에서 직접 추가할 수 있어요.";

        var tradingPaintsMissing = results.Any(r => r.Program.Id == ProgramCatalog.TradingPaintsId && !r.IsInstalled);
        TradingPaintsWarning.Visibility = tradingPaintsMissing ? Visibility.Visible : Visibility.Collapsed;
        ConfirmButton.IsEnabled = found > 0;
    }

    private void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DetectionRowViewModel row)
        {
            Shell.OpenUrl(row.Result.Program.WebsiteUrl);
        }
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
