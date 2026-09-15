using KeyPulse.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KeyPulse.Infrastructure.Persistence;

public sealed class FlushService : IFlushService, IHostedService, IDisposable
{
    private static readonly TimeSpan[] RetryDelays =
    {
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(60)
    };

    private readonly IStatisticsAggregator _aggregator;
    private readonly IStatisticsRepository _repository;
    private readonly DatabaseInitializer _initializer;
    private readonly PersistenceOptions _options;
    private readonly ILogger<FlushService> _logger;
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private int _failures;

    public event Action? Flushed;

    public FlushService(
        IStatisticsAggregator aggregator,
        IStatisticsRepository repository,
        DatabaseInitializer initializer,
        PersistenceOptions options,
        ILogger<FlushService> logger)
    {
        _aggregator = aggregator;
        _repository = repository;
        _initializer = initializer;
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _initializer.Initialize();
        _logger.LogInformation("SQLite persistence started");
        _loop = Task.Run(() => RunAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        if (_loop is not null)
        {
            try
            {
                await _loop.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception)
            {
                // loop is stopping
            }
        }

        await FlushNowAsync(cancellationToken);
    }

    public async Task FlushNowAsync(CancellationToken cancellationToken = default)
    {
        await _flushLock.WaitAsync(cancellationToken);
        try
        {
            var batch = _aggregator.SwapForFlush();
            if (batch.IsEmpty)
            {
                return;
            }

            try
            {
                await _repository.FlushAsync(batch, cancellationToken);
                _failures = 0;
                _logger.LogInformation("Flush succeeded");
                Flushed?.Invoke();
            }
            catch (Exception ex)
            {
                _aggregator.Merge(batch);
                _failures++;
                _logger.LogError(ex, "Flush failed; batch merged back for retry");
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _flushLock.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var delay = NextDelay();
                await Task.Delay(delay, cancellationToken);
                await FlushNowAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private TimeSpan NextDelay()
    {
        if (_failures <= 0)
        {
            return _options.FlushInterval;
        }

        var index = Math.Min(_failures, RetryDelays.Length) - 1;
        return RetryDelays[index];
    }
}
