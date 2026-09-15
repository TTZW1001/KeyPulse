using KeyPulse.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace KeyPulse.App.Services;

public sealed class PowerEventService : IDisposable
{
    private readonly ILifecycleRecovery _recovery;
    private readonly ILogger<PowerEventService> _logger;
    private bool _disposed;

    public PowerEventService(ILifecycleRecovery recovery, ILogger<PowerEventService> logger)
    {
        _recovery = recovery;
        _logger = logger;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionEnding += OnSessionEnding;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionEnding -= OnSessionEnding;
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            RunQuietly(() => _recovery.OnSuspendAsync());
        }
        else if (e.Mode == PowerModes.Resume)
        {
            RunQuietly(() => _recovery.OnResumeAsync());
        }
    }

    private void OnSessionEnding(object sender, SessionEndingEventArgs e)
    {
        RunQuietly(() => _recovery.OnSessionEndingAsync());
    }

    private void RunQuietly(Func<Task> action)
    {
        try
        {
            action().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lifecycle callback failed");
        }
    }
}
