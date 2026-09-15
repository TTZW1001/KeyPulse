using KeyPulse.Core.Statistics;

namespace KeyPulse.Core.Interfaces;

public interface IMouseQuery
{
    Task<MouseTotals> GetMouseTotalsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MouseDayPoint>> GetLast7DaysAsync(
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}
