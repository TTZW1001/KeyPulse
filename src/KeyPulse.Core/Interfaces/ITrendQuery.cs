using KeyPulse.Core.Statistics;

namespace KeyPulse.Core.Interfaces;

public interface ITrendQuery
{
    Task<DateOnly?> GetEarliestDateAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyTrendPoint>> GetDailyAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HourlyPoint>> GetHourlyAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
