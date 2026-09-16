namespace KeyPulse.Infrastructure.Persistence;

public interface IFlushService
{
    event Action? Flushed;

    event Action? WriteErrorChanged;

    bool HasWriteError { get; }

    Task FlushNowAsync(CancellationToken cancellationToken = default);

    Task ClearStatisticsAsync(CancellationToken cancellationToken = default);

    Task ClearPositionDataAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
