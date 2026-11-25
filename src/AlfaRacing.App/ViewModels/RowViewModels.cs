using System.Windows.Media;
using AlfaRacing.App.Infrastructure;
using AlfaRacing.Core.Catalog;
using AlfaRacing.Core.Detection;
using AlfaRacing.Core.Launching;
using AlfaRacing.Core.Models;

namespace AlfaRacing.App.ViewModels;

/// <summary>A row in the main window's program list.</summary>
public sealed class StatusRowViewModel(LaunchItem item) : ObservableObject
{
    private readonly QuickLink? _quickLink = ProgramCatalog.Match(item)?.QuickLink;
    private string _statusText = "확인 중";
    private Brush _statusBrush = Palette.Stopped;

    public LaunchItem Item { get; } = item;

    public string Name { get; } = item.DisplayName();

    public string ToolTipText { get; } = string.IsNullOrWhiteSpace(item.Path) ? "(경로 없음)" : item.Path;

    /// <summary>Extra button for this program, e.g. Trading Paints → "리버리 고르기".</summary>
    public bool HasQuickLink => _quickLink is not null;

    public string QuickLinkLabel => _quickLink is null ? "" : _quickLink.Label + " ↗";

    public string QuickLinkToolTip => _quickLink?.ToolTip ?? "";

    public string? QuickLinkUrl => _quickLink?.Url;

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public Brush StatusBrush
    {
        get => _statusBrush;
        private set => Set(ref _statusBrush, value);
    }

    public void Update(ItemRunState state)
    {
        StatusText = ItemStatus.Label(state);
        StatusBrush = Palette.For(state);
    }
}

/// <summary>A row in the settings window's start list.</summary>
public sealed class EditableItemViewModel(LaunchItem item) : ObservableObject
{
    public LaunchItem Item { get; } = item;

    public string Name => Item.DisplayName();

    public string PathText => string.IsNullOrWhiteSpace(Item.Path) ? "(경로 없음)" : Item.Path;

    public string DelayText => Item.DelaySeconds > 0 ? $"대기 {Item.DelaySeconds}초" : "대기 없음";

    public bool Enabled
    {
        get => Item.Enabled;
        set
        {
            if (Item.Enabled != value)
            {
                Item.Enabled = value;
                Raise();
            }
        }
    }

    public bool StopOnExit
    {
        get => Item.StopOnExit;
        set
        {
            if (Item.StopOnExit != value)
            {
                Item.StopOnExit = value;
                Raise();
            }
        }
    }

    public void Refresh() => Raise(string.Empty);
}

/// <summary>A program card on the "recommended programs" tab.</summary>
public sealed class CatalogRowViewModel(KnownProgram program) : ObservableObject
{
    private DetectionResult? _detection;
    private bool _isInStartList;

    public KnownProgram Program { get; } = program;

    public string Name => Program.Name;

    public string Description => Program.Description;

    public string CategoryLabel => Program.CategoryLabel;

    public Brush CategoryBrush => Palette.For(Program.Category);

    public bool IsInstalled => _detection?.IsInstalled == true;

    public string StatusText => _detection is null
        ? "찾는 중..."
        : _detection.IsInstalled ? $"설치됨 · {_detection.ExecutablePath}" : "설치되지 않음";

    public Brush StatusBrush => _detection is null ? Palette.Stopped : _detection.IsInstalled ? Palette.Running : Palette.Missing;

    public string WebsiteButtonText => IsInstalled ? "홈페이지" : "다운로드";

    /// <summary>Homepage once installed, download page otherwise.</summary>
    public string WebsiteUrl => IsInstalled ? Program.HomepageUrl ?? Program.WebsiteUrl : Program.WebsiteUrl;

    public bool CanAdd => IsInstalled && !_isInStartList;

    public string AddButtonText => _isInStartList ? "추가됨" : "시작 목록에 추가";

    public DetectionResult? Detection => _detection;

    public void Update(DetectionResult? detection, bool isInStartList)
    {
        _detection = detection;
        _isInStartList = isInStartList;
        Raise(string.Empty);
    }
}

/// <summary>A row in the first-run setup window.</summary>
public sealed class DetectionRowViewModel(DetectionResult result) : ObservableObject
{
    private bool _isChecked = result.IsInstalled;

    public DetectionResult Result { get; } = result;

    public string Name => Result.Program.Name;

    public string CategoryLabel => Result.Program.CategoryLabel;

    public Brush CategoryBrush => Palette.For(Result.Program.Category);

    public bool IsInstalled => Result.IsInstalled;

    public bool ShowDownload => !Result.IsInstalled;

    public string StatusText => Result.IsInstalled ? $"찾았어요 · {Result.ExecutablePath}" : "설치되지 않았어요";

    public Brush StatusBrush => Result.IsInstalled ? Palette.Running : Palette.Missing;

    public bool IsChecked
    {
        get => _isChecked;
        set => Set(ref _isChecked, value && Result.IsInstalled);
    }
}

/// <summary>A row in the running programs picker.</summary>
public sealed class RunningAppViewModel(RunningApp app)
{
    public RunningApp App { get; } = app;

    public string DisplayName => App.DisplayName;

    public string ProcessName => App.ProcessName;

    public string ExecutablePath => App.ExecutablePath;
}
