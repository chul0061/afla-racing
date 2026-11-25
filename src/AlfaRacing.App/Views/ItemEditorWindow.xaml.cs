using System.IO;
using System.Windows;
using AlfaRacing.App.Infrastructure;
using AlfaRacing.Core;
using AlfaRacing.Core.Models;
using AlfaRacing.Core.Processes;
using Microsoft.Win32;

namespace AlfaRacing.App.Views;

/// <summary>Adds or edits one start-list program. Works on a copy; read <see cref="Item"/> after OK.</summary>
public partial class ItemEditorWindow : Window
{
    private readonly LaunchItem _item;

    public ItemEditorWindow(LaunchItem item, bool isNew)
    {
        InitializeComponent();
        _item = item.Clone();
        Title = isNew ? "프로그램 추가" : "프로그램 편집";

        NameBox.Text = _item.Name;
        PathBox.Text = _item.Path;
        ArgumentsBox.Text = _item.Arguments;
        DelayBox.Text = _item.DelaySeconds.ToString();
        ProcessNamesBox.Text = string.Join(", ", _item.ProcessNames);
        EnabledCheck.IsChecked = _item.Enabled;
        StopOnExitCheck.IsChecked = _item.StopOnExit;

        Loaded += (_, _) => NameBox.Focus();
    }

    public LaunchItem Item => _item;

    /// <summary>Shows an "open program" dialog. Shortcuts are resolved to the program they point to.</summary>
    public static string? BrowseForProgram(Window owner, string? currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Title = "실행할 프로그램 선택",
            Filter = "프로그램 (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|모든 파일 (*.*)|*.*",
            DereferenceLinks = true,
            CheckFileExists = true,
        };

        var folder = PathHelpers.GetParentDirectory(PathHelpers.Normalize(currentPath));
        if (folder is not null && Directory.Exists(folder))
        {
            dialog.InitialDirectory = folder;
        }

        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var previousDerived = PathHelpers.DeriveProcessName(PathBox.Text) ?? "";
        var path = BrowseForProgram(this, PathBox.Text);
        if (path is null)
        {
            return;
        }

        PathBox.Text = path;
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            NameBox.Text = RunningApps.GetDisplayName(path);
        }

        // Keep hand-written process names; replace ones that only mirrored the old exe name.
        var currentNames = ProcessNamesBox.Text.Trim();
        if (currentNames.Length == 0 || currentNames.Equals(previousDerived, StringComparison.OrdinalIgnoreCase))
        {
            ProcessNamesBox.Text = PathHelpers.DeriveProcessName(path) ?? "";
        }
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        var path = PathBox.Text.Trim().Trim('"').Trim();
        if (path.Length == 0)
        {
            Shell.Warning(this, "실행할 파일이나 주소를 입력해 주세요.");
            PathBox.Focus();
            return;
        }

        if (!int.TryParse(DelayBox.Text.Trim(), out var delay) || delay < 0 || delay > AppSettings.MaxDelaySeconds)
        {
            Shell.Warning(this, $"대기 시간은 0~{AppSettings.MaxDelaySeconds} 사이 숫자로 적어 주세요.");
            DelayBox.Focus();
            return;
        }

        var processNames = new List<string>();
        foreach (var raw in ProcessNamesBox.Text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!ProcessNamePattern.TryParse(raw, out var pattern, out var error))
            {
                Shell.Warning(this, error ?? "프로세스 이름을 확인해 주세요.");
                ProcessNamesBox.Focus();
                return;
            }

            if (!pattern.IsPrefix && ProcessGuard.IsProtected(pattern.Text))
            {
                Shell.Warning(this, $"'{pattern.Text}'은(는) Windows나 {AppInfo.Name}에 꼭 필요한 프로세스라서 쓸 수 없어요.");
                ProcessNamesBox.Focus();
                return;
            }

            if (!processNames.Contains(pattern.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                processNames.Add(pattern.ToString());
            }
        }

        var name = NameBox.Text.Trim();
        _item.Name = name.Length > 0 ? name : PathHelpers.GetFileName(path);
        _item.Path = path;
        _item.Arguments = ArgumentsBox.Text.Trim();
        _item.DelaySeconds = delay;
        _item.ProcessNames = processNames;
        _item.Enabled = EnabledCheck.IsChecked == true;
        _item.StopOnExit = StopOnExitCheck.IsChecked == true;
        DialogResult = true;
    }
}
