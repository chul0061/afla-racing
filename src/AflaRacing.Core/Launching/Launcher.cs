using System.ComponentModel;
using AflaRacing.Core.Models;
using AflaRacing.Core.Processes;

namespace AflaRacing.Core.Launching;

public enum StartOutcome
{
    Started,
    AlreadyRunning,
    Disabled,
    NotFound,
    Cancelled,
    Failed,
}

public sealed record StartStepResult(LaunchItem Item, StartOutcome Outcome, string? Message = null)
{
    public bool IsProblem => Outcome is StartOutcome.NotFound or StartOutcome.Cancelled or StartOutcome.Failed;
}

public enum StopOutcome
{
    Stopped,
    ForceStopped,
    NotRunning,
    AccessDenied,
    Failed,
    NoProcessNames,
}

public sealed record StopTarget(string DisplayName, IReadOnlyList<ProcessNamePattern> Patterns);

public sealed record StopStepResult(StopTarget Target, StopOutcome Outcome, string? Message = null)
{
    public bool IsProblem => Outcome is StopOutcome.AccessDenied or StopOutcome.Failed;
}

/// <param name="Index">Zero-based index of the current step.</param>
public sealed record LaunchProgress(int Index, int Total, string ItemName, string Message);

/// <summary>Starts the start list in order and closes programs in reverse order.</summary>
public sealed class Launcher
{
    private const int ErrorAccessDenied = 5;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorCancelled = 1223; // The user said "No" to the admin (UAC) prompt.
    private const int UnixPermissionDenied = 1; // EPERM

    private static readonly TimeSpan KillWaitTimeout = TimeSpan.FromSeconds(2);

    private readonly IProcessService _processes;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<string, bool> _fileExists;
    private readonly TimeSpan _pollInterval;

    public Launcher(
        IProcessService processes,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Func<string, bool>? fileExists = null,
        TimeSpan? pollInterval = null)
    {
        _processes = processes;
        _delay = delay ?? Task.Delay;
        _fileExists = fileExists ?? File.Exists;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(200);
    }

    public async Task<IReadOnlyList<StartStepResult>> StartAllAsync(
        IReadOnlyList<LaunchItem> items,
        IProgress<LaunchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<StartStepResult>(items.Count);
        var launchedAny = false;

        for (var index = 0; index < items.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[index];
            var name = item.DisplayName();
            void Report(string message) => progress?.Report(new LaunchProgress(index, items.Count, name, message));

            if (!item.Enabled)
            {
                results.Add(new StartStepResult(item, StartOutcome.Disabled));
                continue;
            }

            Report("확인 중");
            var patterns = item.GetProcessPatterns();
            if (patterns.Count > 0 && ProcessNamePattern.AnyMatch(patterns, _processes.GetRunningProcessNames()))
            {
                results.Add(new StartStepResult(item, StartOutcome.AlreadyRunning, "이미 실행 중"));
                Report("이미 실행 중");
                continue;
            }

            var path = PathHelpers.Normalize(item.Path);
            if (path.Length == 0)
            {
                results.Add(new StartStepResult(item, StartOutcome.NotFound, "실행할 파일이 지정되지 않았어요."));
                continue;
            }

            var isUri = PathHelpers.IsUri(path);
            if (!isUri && !_fileExists(path))
            {
                results.Add(new StartStepResult(item, StartOutcome.NotFound, $"파일을 찾을 수 없어요: {path}"));
                Report("파일 없음");
                continue;
            }

            if (launchedAny && item.DelaySeconds > 0)
            {
                Report($"{item.DelaySeconds}초 기다리는 중");
                await _delay(TimeSpan.FromSeconds(item.DelaySeconds), cancellationToken).ConfigureAwait(false);
            }

            Report("실행하는 중");
            try
            {
                _processes.Start(path, item.Arguments, isUri ? null : PathHelpers.GetParentDirectory(path));
                launchedAny = true;
                results.Add(new StartStepResult(item, StartOutcome.Started));
                AppLog.Info($"실행: {name} ({path})");
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
            {
                results.Add(new StartStepResult(item, StartOutcome.Cancelled, "관리자 권한 요청이 취소됐어요."));
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode is ErrorFileNotFound or ErrorPathNotFound)
            {
                results.Add(new StartStepResult(item, StartOutcome.NotFound, $"파일을 찾을 수 없어요: {path}"));
            }
            catch (Exception ex)
            {
                AppLog.Error($"실행 실패: {name} ({path})", ex);
                results.Add(new StartStepResult(item, StartOutcome.Failed, ex.Message));
            }
        }

        return results;
    }

    /// <summary>
    /// What the stop button closes: start-list items marked "stop on exit" in reverse order, then extra processes.
    /// </summary>
    public static IReadOnlyList<StopTarget> BuildStopTargets(AppSettings settings)
    {
        var targets = new List<StopTarget>();
        for (var i = settings.StartItems.Count - 1; i >= 0; i--)
        {
            var item = settings.StartItems[i];
            if (item.StopOnExit)
            {
                targets.Add(new StopTarget(item.DisplayName(), item.GetProcessPatterns()));
            }
        }

        foreach (var extra in settings.ExtraStopProcesses)
        {
            if (ProcessNamePattern.TryParse(extra, out var pattern, out _))
            {
                targets.Add(new StopTarget(pattern.ToString(), [pattern]));
            }
        }

        return targets;
    }

    public async Task<IReadOnlyList<StopStepResult>> StopAllAsync(
        AppSettings settings,
        IProgress<LaunchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var targets = BuildStopTargets(settings);
        var gracefulTimeout = TimeSpan.FromSeconds(Math.Clamp(settings.ForceKillAfterSeconds, 1, AppSettings.MaxForceKillAfterSeconds));
        var results = new List<StopStepResult>(targets.Count);

        for (var index = 0; index < targets.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = targets[index];
            progress?.Report(new LaunchProgress(index, targets.Count, target.DisplayName, "종료하는 중"));
            results.Add(await StopAsync(target, gracefulTimeout, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public async Task<StopStepResult> StopAsync(StopTarget target, TimeSpan gracefulTimeout, CancellationToken cancellationToken = default)
    {
        if (target.Patterns.Count == 0)
        {
            return new StopStepResult(target, StopOutcome.NoProcessNames, "프로세스 이름이 없어서 끌 수 없어요.");
        }

        var processes = new List<IRunningProcess>();
        foreach (var process in _processes.FindProcesses(target.Patterns))
        {
            // Defense in depth: never touch Windows itself or AFLA Racing, whatever the patterns say.
            if (ProcessGuard.IsProtected(process.Name) || process.Id == Environment.ProcessId)
            {
                process.Dispose();
                continue;
            }

            processes.Add(process);
        }

        if (processes.Count == 0)
        {
            return new StopStepResult(target, StopOutcome.NotRunning);
        }

        try
        {
            // 1) Ask nicely (like clicking the window's X button).
            var askedToClose = false;
            foreach (var process in processes)
            {
                askedToClose |= process.RequestClose();
            }

            if (askedToClose)
            {
                await WaitForExitAsync(processes, gracefulTimeout, cancellationToken).ConfigureAwait(false);
            }

            // 2) Force-kill whatever is left (tray apps, apps that only hide their window, hung apps).
            var remaining = processes.Where(p => !p.HasExited).ToList();
            if (remaining.Count == 0)
            {
                AppLog.Info($"종료: {target.DisplayName}");
                return new StopStepResult(target, StopOutcome.Stopped);
            }

            var accessDenied = false;
            string? error = null;
            foreach (var process in remaining)
            {
                try
                {
                    process.Kill();
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode is ErrorAccessDenied or UnixPermissionDenied)
                {
                    accessDenied = true;
                }
                catch (Exception ex)
                {
                    error ??= ex.Message;
                    AppLog.Warn($"강제 종료 실패: {target.DisplayName} (PID {process.Id})", ex);
                }
            }

            await WaitForExitAsync(remaining, KillWaitTimeout, cancellationToken).ConfigureAwait(false);
            if (remaining.All(p => p.HasExited))
            {
                AppLog.Info($"강제 종료: {target.DisplayName}");
                return new StopStepResult(target, StopOutcome.ForceStopped);
            }

            if (accessDenied)
            {
                AppLog.Warn($"권한 부족으로 종료 실패: {target.DisplayName}");
                return new StopStepResult(target, StopOutcome.AccessDenied, "관리자 권한으로 실행 중이라 끌 수 없어요.");
            }

            return new StopStepResult(target, StopOutcome.Failed, error ?? "프로그램이 종료되지 않았어요.");
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    public bool IsAnyRunning(IEnumerable<string> processNames)
    {
        var patterns = ProcessNamePattern.ParseMany(processNames);
        return ProcessNamePattern.AnyMatch(patterns, _processes.GetRunningProcessNames());
    }

    private async Task WaitForExitAsync(IReadOnlyList<IRunningProcess> processes, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var polls = Math.Max(1, (int)Math.Ceiling(timeout.TotalMilliseconds / Math.Max(1, _pollInterval.TotalMilliseconds)));
        for (var i = 0; i < polls; i++)
        {
            if (processes.All(p => p.HasExited))
            {
                return;
            }

            await _delay(_pollInterval, cancellationToken).ConfigureAwait(false);
        }
    }
}
