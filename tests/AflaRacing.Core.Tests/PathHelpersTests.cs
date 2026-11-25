namespace AflaRacing.Core.Tests;

public class PathHelpersTests
{
    [Theory]
    [InlineData("steam://rungameid/266410", true)]
    [InlineData("https://www.tradingpaints.com/install", true)]
    [InlineData(@"C:\Program Files (x86)\iRacing\ui\iRacingUI.exe", false)]
    [InlineData(@"\\server\share\app.exe", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsUri(string input, bool expected) => Assert.Equal(expected, PathHelpers.IsUri(input));

    [Theory]
    [InlineData(@"C:\Program Files (x86)\Britton IT Ltd\CrewChiefV4\CrewChiefV4.exe", "CrewChiefV4")]
    [InlineData(@"""C:\Program Files (x86)\MOZA Pit House\MOZA Pit House.exe""", "MOZA Pit House")]
    [InlineData("/opt/app/thing.EXE", "thing")]
    [InlineData(@"C:\Users\me\Desktop\iRacing.lnk", null)]
    [InlineData(@"C:\tools\start.bat", null)]
    [InlineData("steam://rungameid/266410", null)]
    [InlineData("", null)]
    public void DeriveProcessName(string input, string? expected) => Assert.Equal(expected, PathHelpers.DeriveProcessName(input));

    [Theory]
    [InlineData(@"C:\Program Files (x86)\iRacing\ui\iRacingUI.exe", @"C:\Program Files (x86)\iRacing\ui")]
    [InlineData(@"C:\app.exe", @"C:\")]
    [InlineData("/home/user/app", "/home/user")]
    [InlineData("app.exe", null)]
    [InlineData("", null)]
    public void GetParentDirectory(string input, string? expected) => Assert.Equal(expected, PathHelpers.GetParentDirectory(input));

    [Theory]
    [InlineData(@"""C:\Users\me\AppData\Local\irdashies\Update.exe"" --uninstall", @"C:\Users\me\AppData\Local\irdashies\Update.exe")]
    [InlineData(@"C:\Users\me\AppData\Local\irdashies\Update.exe --uninstall -s", @"C:\Users\me\AppData\Local\irdashies\Update.exe")]
    [InlineData(@"C:\Program Files (x86)\MOZA Pit House\MOZA Pit House.exe,0", @"C:\Program Files (x86)\MOZA Pit House\MOZA Pit House.exe")]
    [InlineData(@"""C:\Program Files\App\app.exe"",0", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\Program Files\App\app.ico", @"C:\Program Files\App\app.ico")]
    [InlineData(@"C:\Program Files\App\app.ico,1", @"C:\Program Files\App\app.ico")]
    [InlineData(@"MsiExec.exe /X{12345678-1234-1234-1234-123456789012}", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ExtractExecutablePath(string? input, string? expected) => Assert.Equal(expected, PathHelpers.ExtractExecutablePath(input));

    [Fact]
    public void Normalize_TrimsQuotes_AndKeepsUris()
    {
        Assert.Equal("steam://rungameid/266410", PathHelpers.Normalize("  steam://rungameid/266410 "));
        Assert.Equal(@"C:\x\y.exe", PathHelpers.Normalize(@" ""C:\x\y.exe"" "));
        Assert.Equal("", PathHelpers.Normalize(null));
    }
}
