using KeyPulse.Core.Interfaces;

namespace KeyPulse.Infrastructure.System;

public sealed class SingleInstanceService : ISingleInstanceService
{
    public const string DefaultMutexName = @"Local\KeyPulse.SingleInstance";
    public const string DefaultShowEventName = @"Local\KeyPulse.ShowWindow";

    private readonly string _mutexName;
    private readonly string _showEventName;
    private readonly CancellationTokenSource _cts = new();
    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private Task? _listener;

    public SingleInstanceService()
        : this(DefaultMutexName, DefaultShowEventName)
    {
    }

    public SingleInstanceService(string mutexName, string showEventName)
    {
        _mutexName = mutexName;
        _showEventName = showEventName;
    }

    public bool IsPrimary { get; private set; }

    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
            IsPrimary = false;
            return false;
        }

        IsPrimary = true;
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, _showEventName);
        return true;
    }

    public void SignalShowWindow()
    {
        using var handle = new EventWaitHandle(false, EventResetMode.AutoReset, _showEventName);
        handle.Set();
    }

    public void StartShowListener(Action onShowRequested)
    {
        if (!IsPrimary)
        {
            return;
        }

        _showEvent ??= new EventWaitHandle(false, EventResetMode.AutoReset, _showEventName);
        _listener = Task.Run(() =>
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    if (_showEvent.WaitOne(TimeSpan.FromMilliseconds(400)))
                    {
                        onShowRequested();
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // shutting down
            }
        });
    }

    public void Dispose()
    {
        _cts.Cancel();
        _showEvent?.Set();
        _listener?.Wait(TimeSpan.FromSeconds(1));
        _showEvent?.Dispose();
        if (_mutex is not null)
        {
            try
            {
                if (IsPrimary)
                {
                    _mutex.ReleaseMutex();
                }
            }
            catch (ApplicationException)
            {
                // not owned
            }

            _mutex.Dispose();
            _mutex = null;
        }

        _cts.Dispose();
    }
}
