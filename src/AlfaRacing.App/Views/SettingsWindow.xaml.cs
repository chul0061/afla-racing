using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AlfaRacing.App.Infrastructure;
using AlfaRacing.App.ViewModels;
using AlfaRacing.Core;
using AlfaRacing.Core.Catalog;
using AlfaRacing.Core.Launching;
using AlfaRacing.Core.Models;
using AlfaRacing.Core.Processes;

namespace AlfaRacing.App.Views;

public enum SettingsTab
{
    StartList = 0,
    Stop = 1,
    Recommended = 2,
    General = 3,
}

/// <summary>Edits a copy of the settings; the main window applies <see cref="Result"/> when the user saves.</summary>
public partial class SettingsWindow : Window
{
    private static readonly int[] ForceKillChoices = [1, 2, 3, 5, 10, 15, 30];

    private readonly AppServices _services;
    private readonly AppSettings _working;
    private readonly ObservableCollection<EditableItemViewModel> _items = [];
    private readonly ObservableCollection<string> _extraProcesses = [];
    private readonly ObservableCollection<CatalogRowViewModel> _catalog = [];

    public SettingsWindow(AppServices services, SettingsTab tab, IReadOnlyList<RunningApp>? runningAppsForPreview = null)
    {
        InitializeComponent();
        _services = services;
        _working = services.Settings.Clone();
        RunningAppsOverride = runningAppsForPreview;

        Title = $"{AppInfo.Name} 설정";
        RunOnStartupCheck.Content = $"Windows를 켤 때 {AppInfo.Name} 자동 실행";
        AboutText.Text = $"{AppInfo.Name} {App.VersionText}\n설정 파일: {_services.Store.FilePath}";

        foreach (var item in _working.StartItems)
        {
            _items.Add(new EditableItemViewModel(item));
        }

        StartList.ItemsSource = _items;

        foreach (var process in _working.ExtraStopProcesses)
        {
            _extraProcesses.Add(process);
        }

        ExtraProcessList.ItemsSource = _extraProcesses;

        var choices = ForceKillChoices.ToList();
        if (!choices.Contains(_working.ForceKillAfterSeconds))
        {
            choices.Add(_working.ForceKillAfterSeconds);
            choices.Sort();
        }

        ForceKillCombo.ItemsSource = choices;
        ForceKillCombo.SelectedItem = _working.ForceKillAfterSeconds;
        ConfirmDrivingCheck.IsChecked = _working.ConfirmStopWhileDriving;
        RunOnStartupCheck.IsChecked = _working.RunOnWindowsStartup;
        MinimizeAfterStartCheck.IsChecked = _working.MinimizeAfterStart;

        foreach (var program in ProgramCatalog.All)
        {
            _catalog.Add(new CatalogRowViewModel(program));
        }

        CatalogList.ItemsSource = _catalog;
        UpdateCatalogRows();

        _services.DetectionUpdated += OnDetectionUpdated;
        Closed += (_, _) => _services.DetectionUpdated -= OnDetectionUpdated;
        Tabs.SelectedIndex = (int)tab;

        Loaded += async (_, _) =>
        {
            if (_services.LastDetection is null && !App.IsSmokeTest)
            {
                await RescanAsync();
            }
        };
    }

    /// <summary>The edited settings (valid after the window returned true).</summary>
    public AppSettings Result => _working;

    /// <summary>Fixed running-programs list used by the smoke test.</summary>
    private IReadOnlyList<RunningApp>? RunningAppsOverride { get; }

    // ---------- Start list ----------

    private EditableItemViewModel? Selected => StartList.SelectedItem as EditableItemViewModel;

    private List<LaunchItem> CurrentItems() => _items.Select(v => v.Item).ToList();

    private void ReplaceItems(List<LaunchItem> items, LaunchItem? select = null)
    {
        _items.Clear();
        foreach (var item in items)
        {
            _items.Add(new EditableItemViewModel(item));
        }

        if (select is not null)
        {
            StartList.SelectedItem = _items.FirstOrDefault(v => ReferenceEquals(v.Item, select));
            StartList.ScrollIntoView(StartList.SelectedItem);
        }

        UpdateCatalogRows();
    }

    private void AddItem(LaunchItem item)
    {
        var items = CurrentItems();
        StartListBuilder.InsertInRecommendedOrder(items, item);
        ReplaceItems(items, item);
    }

    private void OnAddProgramClick(object sender, RoutedEventArgs e)
    {
        var path = ItemEditorWindow.BrowseForProgram(this, null);
        if (path is null)
        {
            return;
        }

        var item = StartListBuilder.CreateFromExecutable(path, displayName: RunningApps.GetDisplayName(path));
        var editor = new ItemEditorWindow(item, isNew: true) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            AddItem(editor.Item);
        }
    }

    private void OnAddFromRunningClick(object sender, RoutedEventArgs e)
    {
        var picker = new ProcessPickerWindow(RunningAppsOverride) { Owner = this };
        if (picker.ShowDialog() != true || picker.SelectedApp is not { } app)
        {
            return;
        }

        var item = StartListBuilder.CreateFromExecutable(app.ExecutablePath, app.ProcessName, app.DisplayName);
        var editor = new ItemEditorWindow(item, isNew: true) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            AddItem(editor.Item);
        }
    }

    private void OnGoToRecommendedClick(object sender, RoutedEventArgs e) => Tabs.SelectedIndex = (int)SettingsTab.Recommended;

    private void OnEditClick(object sender, RoutedEventArgs e) => EditSelected();

    private void OnStartListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Ignore double clicks on the check boxes.
        if (e.OriginalSource is DependencyObject source && FindAncestor<CheckBox>(source) is not null)
        {
            return;
        }

        EditSelected();
    }

    private void EditSelected()
    {
        if (Selected is not { } row)
        {
            Shell.Info(this, "편집할 프로그램을 먼저 골라 주세요.");
            return;
        }

        var editor = new ItemEditorWindow(row.Item, isNew: false) { Owner = this };
        if (editor.ShowDialog() != true)
        {
            return;
        }

        var edited = editor.Item;
        row.Item.Name = edited.Name;
        row.Item.Path = edited.Path;
        row.Item.Arguments = edited.Arguments;
        row.Item.DelaySeconds = edited.DelaySeconds;
        row.Item.ProcessNames = edited.ProcessNames;
        row.Item.Enabled = edited.Enabled;
        row.Item.StopOnExit = edited.StopOnExit;
        row.Refresh();
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row)
        {
            Shell.Info(this, "삭제할 프로그램을 먼저 골라 주세요.");
            return;
        }

        var index = _items.IndexOf(row);
        _items.Remove(row);
        if (_items.Count > 0)
        {
            StartList.SelectedIndex = Math.Min(index, _items.Count - 1);
        }

        UpdateCatalogRows();
    }

    private void OnMoveUpClick(object sender, RoutedEventArgs e) => MoveSelected(-1);

    private void OnMoveDownClick(object sender, RoutedEventArgs e) => MoveSelected(+1);

    private void MoveSelected(int offset)
    {
        if (Selected is not { } row)
        {
            return;
        }

        var index = _items.IndexOf(row);
        var target = index + offset;
        if (target < 0 || target >= _items.Count)
        {
            return;
        }

        _items.Move(index, target);
        StartList.SelectedItem = row;
        StartList.ScrollIntoView(row);
    }

    // ---------- Stop ----------

    private void OnExtraProcessKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddExtraProcess(ExtraProcessInput.Text);
            e.Handled = true;
        }
    }

    private void OnAddExtraProcessClick(object sender, RoutedEventArgs e) => AddExtraProcess(ExtraProcessInput.Text);

    private void OnPickExtraProcessClick(object sender, RoutedEventArgs e)
    {
        var picker = new ProcessPickerWindow(RunningAppsOverride) { Owner = this };
        if (picker.ShowDialog() == true && picker.SelectedApp is { } app)
        {
            AddExtraProcess(app.ProcessName);
        }
    }

    private void AddExtraProcess(string? text)
    {
        if (!ProcessNamePattern.TryParse(text, out var pattern, out var error))
        {
            Shell.Warning(this, error ?? "프로세스 이름을 확인해 주세요.");
            return;
        }

        if (!pattern.IsPrefix && ProcessGuard.IsProtected(pattern.Text))
        {
            Shell.Warning(this, $"'{pattern.Text}'은(는) Windows나 {AppInfo.Name}에 꼭 필요한 프로세스라서 끌 수 없어요.");
            return;
        }

        var value = pattern.ToString();
        if (!_extraProcesses.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            _extraProcesses.Add(value);
        }

        ExtraProcessInput.Clear();
    }

    private void OnRemoveExtraProcessClick(object sender, RoutedEventArgs e)
    {
        if (ExtraProcessList.SelectedItem is string selected)
        {
            _extraProcesses.Remove(selected);
        }
    }

    // ---------- Recommended programs ----------

    private void OnDetectionUpdated(object? sender, EventArgs e) => UpdateCatalogRows();

    private void UpdateCatalogRows()
    {
        var items = CurrentItems();
        foreach (var row in _catalog)
        {
            row.Update(_services.FindDetection(row.Program.Id), StartListBuilder.Contains(items, row.Program.Id));
        }
    }

    private async void OnRescanClick(object sender, RoutedEventArgs e) => await RescanAsync();

    private async Task RescanAsync()
    {
        RescanButton.IsEnabled = false;
        RescanButton.Content = "찾는 중...";
        try
        {
            await _services.DetectAsync();
        }
        catch (Exception ex)
        {
            AppLog.Warn("프로그램 검색 실패", ex);
            Shell.Warning(this, $"설치된 프로그램을 찾다가 오류가 났어요.\n{ex.Message}");
        }
        finally
        {
            RescanButton.Content = "다시 찾기";
            RescanButton.IsEnabled = true;
            UpdateCatalogRows();
        }
    }

    private void OnAddCatalogClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CatalogRowViewModel { Detection.ExecutablePath: { } path } row)
        {
            return;
        }

        AddItem(StartListBuilder.CreateItem(row.Program, path));
    }

    private void OnCatalogWebsiteClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CatalogRowViewModel row)
        {
            Shell.OpenUrl(row.Program.WebsiteUrl);
        }
    }

    // ---------- General ----------

    private void OnRunSetupAgainClick(object sender, RoutedEventArgs e)
    {
        var window = new FirstRunWindow(_services) { Owner = this };
        if (window.ShowDialog() != true)
        {
            return;
        }

        var items = CurrentItems();
        var added = StartListBuilder.Merge(items, window.SelectedResults);
        ReplaceItems(items);
        Tabs.SelectedIndex = (int)SettingsTab.StartList;
        Shell.Info(this, added > 0 ? $"{added}개를 시작 목록에 추가했어요." : "새로 추가할 프로그램이 없었어요.");
    }

    private void OnOpenDataFolderClick(object sender, RoutedEventArgs e) => Shell.OpenFolder(_services.DataDirectory);

    private void OnOpenLogFolderClick(object sender, RoutedEventArgs e) => Shell.OpenFolder(_services.LogDirectory);

    // ---------- Save ----------

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        _working.StartItems = CurrentItems();
        _working.ExtraStopProcesses = _extraProcesses.ToList();
        _working.ForceKillAfterSeconds = ForceKillCombo.SelectedItem is int seconds ? seconds : AppSettings.DefaultForceKillAfterSeconds;
        _working.ConfirmStopWhileDriving = ConfirmDrivingCheck.IsChecked == true;
        _working.RunOnWindowsStartup = RunOnStartupCheck.IsChecked == true;
        _working.MinimizeAfterStart = MinimizeAfterStartCheck.IsChecked == true;
        DialogResult = true;
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}
