using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AlfaRacing.App.Infrastructure;
using AlfaRacing.App.ViewModels;
using AlfaRacing.Core;

namespace AlfaRacing.App.Views;

/// <summary>Lets the user pick one of the currently running programs.</summary>
public partial class ProcessPickerWindow : Window
{
    private readonly IReadOnlyList<RunningApp>? _fixedApps;
    private List<RunningAppViewModel> _all = [];

    /// <param name="fixedApps">Use this list instead of the real running programs (smoke test).</param>
    public ProcessPickerWindow(IReadOnlyList<RunningApp>? fixedApps = null)
    {
        InitializeComponent();
        _fixedApps = fixedApps;
        Loaded += async (_, _) =>
        {
            SearchBox.Focus();
            await LoadAsync();
        };
    }

    public RunningApp? SelectedApp { get; private set; }

    private async Task LoadAsync()
    {
        LoadingText.Visibility = Visibility.Visible;
        AppList.ItemsSource = null;
        try
        {
            var apps = _fixedApps ?? await Task.Run(RunningApps.GetAll);
            _all = apps.Select(a => new RunningAppViewModel(a)).ToList();
            ApplyFilter();
            LoadingText.Visibility = _all.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            LoadingText.Text = "실행 중인 프로그램이 없어요.";
        }
        catch (Exception ex)
        {
            AppLog.Warn("실행 중인 프로그램 목록을 읽지 못했어요.", ex);
            LoadingText.Text = $"목록을 읽지 못했어요: {ex.Message}";
        }
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text.Trim();
        AppList.ItemsSource = query.Length == 0
            ? _all
            : _all.Where(a =>
                    a.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    a.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    a.ExecutablePath.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await LoadAsync();

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e) => Choose();

    private void OnSelectClick(object sender, RoutedEventArgs e) => Choose();

    private void Choose()
    {
        if (AppList.SelectedItem is not RunningAppViewModel selected)
        {
            Shell.Info(this, "프로그램을 먼저 골라 주세요.");
            return;
        }

        SelectedApp = selected.App;
        DialogResult = true;
    }
}
