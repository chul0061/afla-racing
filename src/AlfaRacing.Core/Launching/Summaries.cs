namespace AlfaRacing.Core.Launching;

public sealed record RunSummary(string Headline, IReadOnlyList<string> Problems, string? Tip = null)
{
    public bool HasProblems => Problems.Count > 0;

    /// <summary>Text for a message box: headline, problems and tip.</summary>
    public string ToMessage()
    {
        var parts = new List<string> { Headline };
        if (Problems.Count > 0)
        {
            parts.Add(string.Join("\n", Problems));
        }

        if (Tip is not null)
        {
            parts.Add(Tip);
        }

        return string.Join("\n\n", parts);
    }
}

/// <summary>Turns start/stop results into short Korean status messages.</summary>
public static class Summaries
{
    public static RunSummary ForStart(IReadOnlyList<StartStepResult> results)
    {
        var started = results.Count(r => r.Outcome == StartOutcome.Started);
        var alreadyRunning = results.Count(r => r.Outcome == StartOutcome.AlreadyRunning);
        var problems = results
            .Where(r => r.IsProblem)
            .Select(r => $"• {r.Item.DisplayName()}: {r.Message}")
            .ToList();

        if (started == 0 && alreadyRunning == 0 && problems.Count == 0)
        {
            return new RunSummary("켤 프로그램이 없었어요.", problems);
        }

        var parts = new List<string>();
        if (started > 0)
        {
            parts.Add($"{started}개 실행");
        }

        if (alreadyRunning > 0)
        {
            parts.Add($"{alreadyRunning}개는 이미 실행 중");
        }

        if (problems.Count > 0)
        {
            parts.Add($"{problems.Count}개 실패");
        }

        var headline = (problems.Count > 0 ? "시작했지만 문제가 있어요" : "레이스 준비 완료") + " · " + string.Join(" · ", parts);
        var tip = results.Any(r => r.Outcome == StartOutcome.NotFound)
            ? "설정에서 경로를 다시 지정하거나, 프로그램을 다시 설치해 주세요."
            : null;
        return new RunSummary(headline, problems, tip);
    }

    public static RunSummary ForStop(IReadOnlyList<StopStepResult> results)
    {
        var stopped = results.Count(r => r.Outcome is StopOutcome.Stopped or StopOutcome.ForceStopped);
        var problems = results
            .Where(r => r.IsProblem || r.Outcome == StopOutcome.NoProcessNames)
            .Select(r => $"• {r.Target.DisplayName}: {r.Message}")
            .ToList();

        string headline;
        if (stopped == 0 && problems.Count == 0)
        {
            headline = "꺼야 할 프로그램이 실행 중이 아니었어요.";
        }
        else if (problems.Count == 0)
        {
            headline = $"모두 종료했어요 · {stopped}개 종료";
        }
        else
        {
            headline = $"일부를 끄지 못했어요 · {stopped}개 종료 · {problems.Count}개 실패";
        }

        string? tip = null;
        if (results.Any(r => r.Outcome == StopOutcome.AccessDenied))
        {
            tip = $"{AppInfo.Name}을(를) 마우스 오른쪽 버튼 → '관리자 권한으로 실행'으로 켜면 끌 수 있어요.";
        }
        else if (results.Any(r => r.Outcome == StopOutcome.NoProcessNames))
        {
            tip = "설정 → 시작 목록에서 해당 프로그램을 편집해 프로세스 이름을 넣어 주세요.";
        }

        return new RunSummary(headline, problems, tip);
    }
}
