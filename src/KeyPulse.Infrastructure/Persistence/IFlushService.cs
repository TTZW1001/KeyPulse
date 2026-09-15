namespace KeyPulse.Infrastructure.Persistence;

public interface IFlushService
{
    Task FlushNowAsync(CancellationToken cancellationToken = default);
}
