using KeyPulse.Core.Statistics;

namespace KeyPulse.Infrastructure.Persistence;

public interface IStatisticsRepository
{
    Task FlushAsync(StatisticsBatch batch, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyKeyRow>> GetKeyStatsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyShortcutRow>> GetShortcutStatsAsync(
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

    Task<IReadOnlyList<DailyAppRow>> GetAppStatsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<DateOnly?> GetEarliestStatDateAsync(CancellationToken cancellationToken = default);

    Task ClearStatisticsAsync(CancellationToken cancellationToken = default);

    Task ClearStatisticsRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task ClearPositionDataAsync(CancellationToken cancellationToken = default);

    Task ClearOccupancyDataAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task PrunePositionDataAsync(DateOnly beforeDate, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    bool TryPing();
}
