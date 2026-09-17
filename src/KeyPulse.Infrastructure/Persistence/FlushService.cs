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
    private readonly IUserSettings? _settings;
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private int _failures;
    private int _writeError;
    private DateOnly? _lastRetentionCleanup;

    public DateTimeOffset? LastSuccessfulFlush { get; private set; }

    public event Action? Flushed;

    public event Action? WriteErrorChanged;

    public bool HasWriteError => Volatile.Read(ref _writeError) != 0;

    public FlushService(
        IStatisticsAggregator aggregator,
        IStatisticsRepository repository,
        DatabaseInitializer initializer,
        PersistenceOptions options,
        ILogger<FlushService> logger,
        IUserSettings? settings = null)
    {
        _aggregator = aggregator;
        _repository = repository;
        _initializer = initializer;
        _options = options;
        _settings = settings;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!TryInitialize())
        {
            _failures = 1;
            SetWriteError();
        }
        else
        {
            _logger.LogInformation("SQLite persistence started");
        }

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
            if (HasWriteError && !TryInitialize())
            {
                _failures++;
                return;
            }

            var batch = _aggregator.SwapForFlush();
            if (batch.IsEmpty)
            {
                if (HasWriteError && _repository.TryPing())
                {
                    _failures = 0;
                    ClearWriteError();
                }

                return;
            }

            try
            {
                await _repository.FlushAsync(batch, cancellationToken);
            }
            catch (Exception ex)
            {
                _aggregator.Merge(batch);
                _failures++;
                SetWriteError();
                _logger.LogError(ex, "Flush failed; batch merged back for retry");
                return;
            }

            _failures = 0;
            ClearWriteError();
            LastSuccessfulFlush = DateTimeOffset.Now;
            try
            {
                await ApplyRetentionPolicyAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Position-data retention cleanup failed; persisted statistics remain valid");
            }
            _logger.LogInformation("Flush succeeded");
            RaiseSafely(Flushed, "Flush notification failed");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public async Task ClearStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await _flushLock.WaitAsync(cancellationToken);
        try
        {
            await _repository.ClearStatisticsAsync(cancellationToken);
            _aggregator.Clear();
            _failures = 0;
            ClearWriteError();
            _logger.LogInformation("Statistics cleared");
            RaiseSafely(Flushed, "Clear notification failed");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public async Task ClearStatisticsRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        await _flushLock.WaitAsync(cancellationToken);
        try
        {
            await _repository.ClearStatisticsRangeAsync(from, to, cancellationToken);
            _aggregator.Clear();
            _logger.LogInformation("Statistics cleared for {From} through {To}", from, to);
            RaiseSafely(Flushed, "Range-clear notification failed");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public async Task ClearPositionDataAsync(CancellationToken cancellationToken = default)
    {
        await _flushLock.WaitAsync(cancellationToken);
        try
        {
            await _repository.ClearPositionDataAsync(cancellationToken);
            _aggregator.ClearPositionData();
            _logger.LogInformation("Pointer position statistics cleared");
            RaiseSafely(Flushed, "Clear-position notification failed");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public async Task RunExclusiveAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _flushLock.WaitAsync(cancellationToken);
        try
        {
            await operation(cancellationToken);
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public async Task ClearOccupancyDataAsync(CancellationToken cancellationToken = default)
    {
        await _flushLock.WaitAsync(cancellationToken);
        try
        {
            await _repository.ClearOccupancyDataAsync(cancellationToken);
            _aggregator.ClearOccupancyData();
            RaiseSafely(Flushed, "Clear-occupancy notification failed");
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

    private bool TryInitialize()
    {
        try
        {
            _initializer.Initialize();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database initialize failed");
            return false;
        }
    }

    private void SetWriteError()
    {
        if (Interlocked.Exchange(ref _writeError, 1) == 0)
        {
            RaiseSafely(WriteErrorChanged, "Write-error notification failed");
        }
    }

    private void ClearWriteError()
    {
        if (Interlocked.Exchange(ref _writeError, 0) != 0)
        {
            RaiseSafely(WriteErrorChanged, "Write-error notification failed");
        }
    }

    private void RaiseSafely(Action? handlers, string message)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (Action handler in handlers.GetInvocationList())
        {
            try
            {
                handler();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, message);
            }
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

    private async Task ApplyRetentionPolicyAsync(CancellationToken cancellationToken)
    {
        var days = _settings?.PositionRetentionDays ?? 0;
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (days <= 0 || _lastRetentionCleanup == today)
        {
            return;
        }

        await _repository.PrunePositionDataAsync(today.AddDays(-days + 1), cancellationToken);
        _lastRetentionCleanup = today;
    }
}
