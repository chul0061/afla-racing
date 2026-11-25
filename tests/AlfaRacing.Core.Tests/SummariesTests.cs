using AlfaRacing.Core.Catalog;
using AlfaRacing.Core.Launching;
using AlfaRacing.Core.Models;
using AlfaRacing.Core.Processes;

namespace AlfaRacing.Core.Tests;

public class SummariesTests
{
    private static LaunchItem Item(string name) => new() { Name = name, Path = $@"C:\{name}\{name}.exe" };

    private static StopTarget Target(string name)
    {
        ProcessNamePattern.TryParse(name, out var pattern, out _);
        return new StopTarget(name, [pattern]);
    }

    [Fact]
    public void Start_AllGood()
    {
        var summary = Summaries.ForStart(
        [
            new StartStepResult(Item("A"), StartOutcome.Started),
            new StartStepResult(Item("B"), StartOutcome.Started),
            new StartStepResult(Item("C"), StartOutcome.AlreadyRunning),
            new StartStepResult(Item("D"), StartOutcome.Disabled),
        ]);

        Assert.False(summary.HasProblems);
        Assert.Equal("레이스 준비 완료 · 2개 실행 · 1개는 이미 실행 중", summary.Headline);
        Assert.Null(summary.Tip);
    }

    [Fact]
    public void Start_WithMissingFile_ListsProblemAndTip()
    {
        var summary = Summaries.ForStart(
        [
            new StartStepResult(Item("A"), StartOutcome.Started),
            new StartStepResult(Item("Bloops"), StartOutcome.NotFound, "파일을 찾을 수 없어요: C:\\Bloops\\Bloops.exe"),
        ]);

        Assert.True(summary.HasProblems);
        Assert.StartsWith("시작했지만 문제가 있어요", summary.Headline);
        Assert.Contains("Bloops", Assert.Single(summary.Problems));
        Assert.NotNull(summary.Tip);
        Assert.Contains(summary.Tip!, summary.ToMessage());
    }

    [Fact]
    public void Start_Nothing()
    {
        var summary = Summaries.ForStart([new StartStepResult(Item("A"), StartOutcome.Disabled)]);
        Assert.Equal("켤 프로그램이 없었어요.", summary.Headline);
    }

    [Fact]
    public void Stop_Variants()
    {
        var allGood = Summaries.ForStop(
        [
            new StopStepResult(Target("A"), StopOutcome.Stopped),
            new StopStepResult(Target("B"), StopOutcome.ForceStopped),
            new StopStepResult(Target("C"), StopOutcome.NotRunning),
        ]);
        Assert.Equal("모두 종료했어요 · 2개 종료", allGood.Headline);

        var nothing = Summaries.ForStop([new StopStepResult(Target("A"), StopOutcome.NotRunning)]);
        Assert.Equal("꺼야 할 프로그램이 실행 중이 아니었어요.", nothing.Headline);

        var denied = Summaries.ForStop(
        [
            new StopStepResult(Target("A"), StopOutcome.Stopped),
            new StopStepResult(Target("MOZA Pit House"), StopOutcome.AccessDenied, "관리자 권한으로 실행 중이라 끌 수 없어요."),
        ]);
        Assert.True(denied.HasProblems);
        Assert.Contains("관리자 권한", denied.Tip);
    }

    [Fact]
    public void CreateFromExecutable_RecognizesCatalogPrograms()
    {
        var known = StartListBuilder.CreateFromExecutable(@"D:\Apps\CrewChief\CrewChiefV4.exe", "CrewChiefV4", "Crew Chief V4");
        Assert.Equal(ProgramCatalog.CrewChiefId, known.CatalogId);
        Assert.Equal("CrewChief", known.Name);

        var custom = StartListBuilder.CreateFromExecutable(@"D:\Apps\SimHub\SimHubWPF.exe", "SimHubWPF", "SimHub");
        Assert.Null(custom.CatalogId);
        Assert.Equal("SimHub", custom.Name);
        Assert.Equal(["SimHubWPF"], custom.ProcessNames);

        var shortcut = StartListBuilder.CreateFromExecutable(@"C:\Users\me\Desktop\Thing.lnk");
        Assert.Equal("Thing.lnk", shortcut.Name);
        Assert.Empty(shortcut.ProcessNames);
    }
}
