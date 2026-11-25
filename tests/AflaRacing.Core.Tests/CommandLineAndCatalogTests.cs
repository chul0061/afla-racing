using AflaRacing.Core.Catalog;
using AflaRacing.Core.Models;

namespace AflaRacing.Core.Tests;

public class CommandLineAndCatalogTests
{
    [Fact]
    public void Parse_QuickActions()
    {
        Assert.Equal(QuickAction.Start, CommandLineOptions.Parse(["--start"]).QuickAction);
        Assert.Equal(QuickAction.Stop, CommandLineOptions.Parse(["--STOP"]).QuickAction);
        Assert.Equal(QuickAction.None, CommandLineOptions.Parse([]).QuickAction);
    }

    [Fact]
    public void Parse_OtherOptions()
    {
        var options = CommandLineOptions.Parse(["--autostart", "--data-dir", @"C:\data", "--smoke-test", @"C:\out"]);

        Assert.True(options.AutoStart);
        Assert.Equal(@"C:\data", options.DataDirectory);
        Assert.Equal(@"C:\out", options.SmokeTestOutput);
        Assert.Equal(QuickAction.None, options.QuickAction);
    }

    [Fact]
    public void Parse_IgnoresMissingValues()
    {
        var options = CommandLineOptions.Parse(["--data-dir"]);
        Assert.Null(options.DataDirectory);
    }

    [Fact]
    public void TradingPaints_HasLiveryButton_ToItsHomepage()
    {
        var tp = ProgramCatalog.TradingPaints;

        Assert.NotNull(tp.QuickLink);
        Assert.Equal("리버리 고르기", tp.QuickLink!.Label);
        Assert.Equal("https://www.tradingpaints.com/", tp.QuickLink.Url);
        Assert.Equal("https://www.tradingpaints.com/", tp.HomepageUrl);
        Assert.Equal("https://www.tradingpaints.com/install", tp.WebsiteUrl);
    }

    [Fact]
    public void Match_FindsCatalogProgram_ByIdOrExeName()
    {
        Assert.Equal(ProgramCatalog.TradingPaintsId, ProgramCatalog.Match(new LaunchItem { CatalogId = ProgramCatalog.TradingPaintsId })?.Id);
        Assert.Equal(ProgramCatalog.TradingPaintsId, ProgramCatalog.Match(new LaunchItem { Path = @"D:\Apps\TP\Trading Paints.exe" })?.Id);
        Assert.Equal(ProgramCatalog.IRacingId, ProgramCatalog.Match(new LaunchItem { Path = @"C:\iRacing\ui\iracingui.exe" })?.Id);
        Assert.Null(ProgramCatalog.Match(new LaunchItem { Path = @"C:\Tools\SimHubWPF.exe" }));
        Assert.Null(ProgramCatalog.Match(new LaunchItem { Path = "steam://rungameid/266410" }));
    }
}
