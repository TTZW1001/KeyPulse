using KeyPulse.Core.Statistics;

namespace KeyPulse.Core.Interfaces;

public interface IDashboardQuery
{
    Task<DashboardToday> GetTodayAsync(CancellationToken cancellationToken = default);

    Task<DashboardToday> GetTodayAsync(DateOnly date, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyTrendPoint>> GetLast7DaysAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyTrendPoint>> GetLast7DaysAsync(DateOnly endDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HourlyPoint>> GetTodayHourlyAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HourlyPoint>> GetTodayHourlyAsync(DateOnly date, CancellationToken cancellationToken = default);

    Task<DashboardInsights> GetInsightsAsync(DateOnly date, CancellationToken cancellationToken = default);
}
