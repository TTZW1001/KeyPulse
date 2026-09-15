using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace KeyPulse.Infrastructure.Hosting;

public sealed class LifecycleRecovery : ILifecycleRecovery
{
    private readonly IInputCapture _capture;
    private readonly IFlushService _flush;
    private readonly IStatisticsAggregator _aggregator;
    private readonly IForegroundAppCache? _foreground;
    private readonly ILogger<LifecycleRecovery> _logger;

    public LifecycleRecovery(
        IInputCapture capture,
        IFlushService flush,
        IStatisticsAggregator aggregator,
        ILogger<LifecycleRecovery> logger,
        IForegroundAppCache? foreground = null)
    {
        _capture = capture;
        _flush = flush;
        _aggregator = aggregator;
        _logger = logger;
        _foreground = foreground;
    }

    public async Task OnSuspendAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Power suspend: flushing");
        await FlushQuietlyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task OnResumeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Power resume: verifying capture and sampler");
        if (!_capture.IsListening)
        {
            await _capture.StartAsync(cancellationToken).ConfigureAwait(false);
            if (_capture.IsListening && _capture.Error is null)
            {
                if (_aggregator.State == TrackingState.Error)
                {
                    _aggregator.SetState(TrackingState.Running);
                }
            }
            else
            {
                _logger.LogError("Raw Input did not resume: {Error}", _capture.Error);
                _aggregator.SetState(TrackingState.Error);
            }
        }

        try
        {
            _foreground?.RefreshSample();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Foreground sampler refresh after resume failed");
        }
    }

    public async Task OnSessionEndingAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Session ending: stopping capture and flushing");
        try
        {
            await _capture.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stop capture on session ending failed");
        }

        await FlushQuietlyAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task FlushQuietlyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _flush.FlushNowAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Flush during lifecycle event failed");
        }
    }
}
