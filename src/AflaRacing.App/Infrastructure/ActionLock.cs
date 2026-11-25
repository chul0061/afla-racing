using System.Threading;

namespace AflaRacing.App.Infrastructure;

/// <summary>
/// Makes sure only one start/stop runs at a time across processes (main window and desktop shortcuts),
/// so double-clicking a shortcut twice can't launch every program twice.
/// </summary>
public sealed class ActionLock : IDisposable
{
    private const string SemaphoreName = @"Local\AFLARacing.Action";
    private readonly Semaphore _semaphore;
    private bool _released;

    private ActionLock(Semaphore semaphore) => _semaphore = semaphore;

    /// <summary>Null when another start/stop is already running.</summary>
    public static ActionLock? TryAcquire()
    {
        var semaphore = new Semaphore(1, 1, SemaphoreName);
        if (semaphore.WaitOne(0))
        {
            return new ActionLock(semaphore);
        }

        semaphore.Dispose();
        return null;
    }

    public void Dispose()
    {
        if (_released)
        {
            return;
        }

        _released = true;
        try
        {
            _semaphore.Release();
        }
        catch (Exception)
        {
            // Best effort.
        }

        _semaphore.Dispose();
    }
}
