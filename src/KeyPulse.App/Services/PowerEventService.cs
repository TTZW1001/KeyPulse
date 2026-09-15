using KeyPulse.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace KeyPulse.App.Services;

public sealed class PowerEventService : IDisposable
{
    private readonly IFlushService _flush;
    private readonly ILogger<PowerEventService> _logger;
    private bool _disposed;

    public PowerEventService(IFlushService flush, ILogger<PowerEventService> logger)
    {
        _flush = flush;
        _logger = logger;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            _logger.LogInformation("Power suspend: flushing");
            try
            {
                _flush.FlushNowAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Flush on suspend failed");
            }
        }
        else if (e.Mode == PowerModes.Resume)
        {
            _logger.LogInformation("Power resume: capture and flush timer should still be running");
        }
    }
}
