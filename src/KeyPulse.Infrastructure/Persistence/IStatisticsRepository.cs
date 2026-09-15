using KeyPulse.Core.Statistics;

namespace KeyPulse.Infrastructure.Persistence;

public interface IStatisticsRepository
{
    Task FlushAsync(StatisticsBatch batch, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyKeyRow>> GetKeyStatsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyMouseRow>> GetMouseStatsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HourlyRow>> GetHourlyAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<DashboardSummary> GetDashboardAsync(
        DateOnly date,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyKeyRow>> GetTrendAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyKeyRow>> GetAppStatsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<DateOnly?> GetEarliestStatDateAsync(CancellationToken cancellationToken = default);
}
