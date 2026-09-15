namespace KeyPulse.Infrastructure.Persistence;

public interface IFlushService
{
    event Action? Flushed;

    Task FlushNowAsync(CancellationToken cancellationToken = default);

    Task ClearStatisticsAsync(CancellationToken cancellationToken = default);
}
