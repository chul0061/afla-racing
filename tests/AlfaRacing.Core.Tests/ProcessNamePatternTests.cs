using AlfaRacing.Core.Processes;

namespace AlfaRacing.Core.Tests;

public class ProcessNamePatternTests
{
    [Theory]
    [InlineData("CrewChiefV4", "CrewChiefV4", false)]
    [InlineData("CrewChiefV4.exe", "CrewChiefV4", false)]
    [InlineData("  \"MOZA Pit House.exe\" ", "MOZA Pit House", false)]
    [InlineData(@"C:\Program Files (x86)\iRacing\ui\iRacingUI.exe", "iRacingUI", false)]
    [InlineData("iRacingSim*", "iRacingSim", true)]
    [InlineData("iRacingSim*.exe", "iRacingSim", true)]
    public void TryParse_Valid(string input, string expectedText, bool expectedPrefix)
    {
        Assert.True(ProcessNamePattern.TryParse(input, out var pattern, out var error));
        Assert.Null(error);
        Assert.Equal(expectedText, pattern.Text);
        Assert.Equal(expectedPrefix, pattern.IsPrefix);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("*")]
    [InlineData("ab*")]
    [InlineData("i*Racing")]
    [InlineData("iRac?ng")]
    [InlineData(".exe")]
    public void TryParse_Invalid(string? input)
    {
        Assert.False(ProcessNamePattern.TryParse(input, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Theory]
    [InlineData("iRacingUI", "iRacingUI", true)]
    [InlineData("iRacingUI", "iracingui", true)]
    [InlineData("iRacingUI", "iRacingUI2", false)]
    [InlineData("iRacingSim*", "iRacingSim64DX11", true)]
    [InlineData("iRacingSim*", "IRACINGSIM64DX12", true)]
    [InlineData("iRacingSim*", "iRacingUI", false)]
    public void Matches(string pattern, string processName, bool expected)
    {
        Assert.True(ProcessNamePattern.TryParse(pattern, out var parsed, out _));
        Assert.Equal(expected, parsed.Matches(processName));
    }

    [Fact]
    public void ParseMany_SkipsInvalid_AndDuplicatesIgnoringCase()
    {
        var patterns = ProcessNamePattern.ParseMany(["CrewChiefV4", "crewchiefv4.exe", "", "*", "iRacingSim*"]);
        Assert.Equal(2, patterns.Count);
    }

    [Fact]
    public void ProtectedProcesses_IncludeWindowsAndSelf()
    {
        Assert.True(ProcessGuard.IsProtected("explorer"));
        Assert.True(ProcessGuard.IsProtected("SVCHOST"));
        Assert.True(ProcessGuard.IsProtected(AppInfo.ProcessName));
        Assert.True(ProcessGuard.IsProtected(""));
        Assert.False(ProcessGuard.IsProtected("CrewChiefV4"));
    }
}
