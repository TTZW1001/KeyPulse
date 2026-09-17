namespace KeyPulse.Infrastructure.Persistence;

public interface IFlushService
{
    event Action? Flushed;

    event Action? WriteErrorChanged;

    bool HasWriteError { get; }

    DateTimeOffset? LastSuccessfulFlush => null;

    Task FlushNowAsync(CancellationToken cancellationToken = default);

    Task ClearStatisticsAsync(CancellationToken cancellationToken = default);

    Task ClearStatisticsRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task ClearPositionDataAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task ClearOccupancyDataAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task RunExclusiveAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default) => operation(cancellationToken);
}
