using System.Threading;

namespace AlfaRacing.App.Infrastructure;

/// <summary>
/// Keeps one ALFA Racing per Windows session. A second launch just brings the first window to the front.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\ALFARacing.SingleInstance";
    private const string EventName = @"Local\ALFARacing.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private volatile bool _disposed;

    private SingleInstance(Mutex mutex, EventWaitHandle activate)
    {
        _mutex = mutex;
        _activate = activate;
        var listener = new Thread(Listen) { IsBackground = true, Name = "SingleInstanceListener" };
        listener.Start();
    }

    /// <summary>Raised on a background thread when another launch asked us to show the window.</summary>
    public event Action? ActivationRequested;

    /// <summary>Returns null when another instance already runs (after asking it to come to the front).</summary>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            try
            {
                using var existing = EventWaitHandle.OpenExisting(EventName);
                existing.Set();
            }
            catch (Exception)
            {
                // The other instance is starting or closing; nothing to do.
            }

            return null;
        }

        var activate = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        return new SingleInstance(mutex, activate);
    }

    private void Listen()
    {
        try
        {
            while (!_disposed && _activate.WaitOne())
            {
                if (!_disposed)
                {
                    ActivationRequested?.Invoke();
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Shutting down.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _activate.Set(); // Wake the listener so it can exit.
            _mutex.ReleaseMutex();
        }
        catch (Exception)
        {
            // Best effort.
        }

        _mutex.Dispose();
    }
}
