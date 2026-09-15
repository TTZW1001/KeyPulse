using KeyPulse.Core.Statistics;

namespace KeyPulse.Core.Interfaces;

public interface IAppQuery
{
    Task<IReadOnlyList<AppRankRow>> GetRankingAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
