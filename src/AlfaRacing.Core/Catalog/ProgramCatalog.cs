namespace AlfaRacing.Core.Catalog;

/// <summary>Programs ALFA Racing recommends and can find automatically, in the recommended start order.</summary>
public static class ProgramCatalog
{
    public const string MozaPitHouseId = "moza-pit-house";
    public const string TradingPaintsId = "trading-paints";
    public const string CrewChiefId = "crewchief";
    public const string IrDashiesId = "irdashies";
    public const string BloopsId = "bloops";
    public const string IRacingId = "iracing";

    /// <summary>Processes that mean the iRacing simulator itself (a session) is running.</summary>
    public static IReadOnlyList<string> DrivingProcessNames { get; } = ["iRacingSim*"];

    public static IReadOnlyList<KnownProgram> All { get; } =
    [
        new KnownProgram
        {
            Id = MozaPitHouseId,
            Name = "MOZA Pit House",
            Category = ProgramCategory.Hardware,
            Description = "MOZA 휠베이스·페달 설정 프로그램이에요. 장비가 먼저 잡히도록 가장 먼저 켜요.",
            WebsiteUrl = "https://mozaracing.com/pages/pit-house",
            ExeNames = ["MOZA Pit House.exe"],
            SearchDirectories =
            [
                @"%ProgramFiles(x86)%\MOZA Pit House",
                @"%ProgramFiles%\MOZA Pit House",
            ],
            UninstallNames = ["MOZA Pit House"],
            DefaultDelaySeconds = 0,
            Order = 10,
        },
        new KnownProgram
        {
            Id = TradingPaintsId,
            Name = "Trading Paints",
            Category = ProgramCategory.Required,
            Description = "다른 드라이버들의 커스텀 페인트를 받아와요. 없으면 다른 차들이 기본 도색으로 보여요.",
            WebsiteUrl = "https://www.tradingpaints.com/install",
            ExeNames = ["Trading Paints.exe", "TradingPaints.exe", "Trading Paints Downloader.exe"],
            SearchDirectories =
            [
                @"%ProgramFiles(x86)%\Trading Paints",
                @"%ProgramFiles%\Trading Paints",
                @"%LOCALAPPDATA%\Programs\Trading Paints",
                @"%LOCALAPPDATA%\Trading Paints",
            ],
            UninstallNames = ["Trading Paints"],
            DefaultDelaySeconds = 2,
            Order = 20,
        },
        new KnownProgram
        {
            Id = CrewChiefId,
            Name = "CrewChief",
            Category = ProgramCategory.Recommended,
            Description = "음성 스포터·크루 치프예요. 옆 차 위치, 연료, 타이어, 페널티 등을 음성으로 알려줘요.",
            WebsiteUrl = "https://thecrewchief.org/",
            ExeNames = ["CrewChiefV4.exe"],
            SearchDirectories =
            [
                @"%ProgramFiles(x86)%\Britton IT Ltd\CrewChiefV4",
                @"%ProgramFiles%\Britton IT Ltd\CrewChiefV4",
            ],
            UninstallNames = ["CrewChief"],
            DefaultDelaySeconds = 1,
            Order = 30,
        },
        new KnownProgram
        {
            Id = IrDashiesId,
            Name = "irDashies",
            Category = ProgramCategory.Recommended,
            Description = "무료 오픈소스 iRacing 오버레이예요. 상대 간격, 순위표, 입력 그래프 등을 화면에 띄워줘요.",
            WebsiteUrl = "https://irdashies.com/",
            ExeNames = ["irdashies.exe"],
            SearchDirectories =
            [
                @"%LOCALAPPDATA%\irdashies",
                @"%LOCALAPPDATA%\Programs\irdashies",
                @"%ProgramFiles%\irdashies",
            ],
            UninstallNames = ["irdashies"],
            DefaultDelaySeconds = 1,
            Order = 40,
        },
        new KnownProgram
        {
            Id = BloopsId,
            Name = "Bloops",
            Category = ProgramCategory.Recommended,
            Description = "Garage 61 레퍼런스 랩과 비교해서 브레이킹 포인트를 소리로 알려줘요.",
            WebsiteUrl = "https://bloo.ps/",
            ExeNames = ["Bloops.exe"],
            SearchDirectories =
            [
                @"%LOCALAPPDATA%\Programs\Bloops",
                @"%LOCALAPPDATA%\Bloops",
                @"%ProgramFiles%\Bloops",
                @"%ProgramFiles(x86)%\Bloops",
            ],
            UninstallNames = ["Bloops"],
            DefaultDelaySeconds = 1,
            Order = 50,
        },
        new KnownProgram
        {
            Id = IRacingId,
            Name = "iRacing",
            Category = ProgramCategory.Game,
            Description = "iRacing UI를 켜요. 다른 프로그램이 다 켜진 뒤 마지막에 실행돼요.",
            WebsiteUrl = "https://www.iracing.com/",
            ExeNames = ["iRacingUI.exe"],
            SearchDirectories =
            [
                @"%ProgramFiles(x86)%\iRacing\ui",
                @"%ProgramFiles%\iRacing\ui",
                @"%ProgramFiles(x86)%\Steam\steamapps\common\iRacing\ui",
            ],
            UninstallNames = ["iRacing"],
            ProcessNames = ["iRacingUI", "iRacingSim*"],
            AlternativeLaunchUri = "steam://rungameid/266410",
            DefaultDelaySeconds = 2,
            Order = 90,
        },
    ];

    public static KnownProgram? Find(string? id) =>
        id is null ? null : All.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static KnownProgram TradingPaints => Find(TradingPaintsId)!;
}
