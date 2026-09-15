using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KeyPulse.Infrastructure.Input;

public sealed class ForegroundAppSampler : IForegroundAppCache, IHostedService, IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly IForegroundAppResolver _resolver;
    private readonly IClock _clock;
    private readonly ILogger<ForegroundAppSampler> _logger;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private ForegroundApp? _current;
    private ForegroundApp? _previous;
    private DateTimeOffset? _previousTime;
    private int _disposed;

    public ForegroundAppSampler(
        IForegroundAppResolver resolver,
        IClock clock,
        ILogger<ForegroundAppSampler> logger)
    {
        _resolver = resolver;
        _clock = clock;
        _logger = logger;
    }

    public ForegroundApp? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public event Action<ForegroundTick>? Sampled;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        SampleOnce();
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
    }

    public void SampleOnce()
    {
        ForegroundApp? app = null;
        try
        {
            app = _resolver.TryResolve();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Foreground app resolve failed; skipping sample");
        }

        ForegroundTick? tick = null;
        lock (_gate)
        {
            var now = _clock.Now;
            if (_previousTime is { } previousTime)
            {
                tick = new ForegroundTick(_previous, now - previousTime, now);
            }

            _previous = app;
            _previousTime = now;
            _current = app;
        }

        if (tick is not null)
        {
            Sampled?.Invoke(tick);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                SampleOnce();
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }
}
