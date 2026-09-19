using KeyPulse.Core.Statistics;

namespace KeyPulse.Core.Interfaces;

public interface IReportQuery
{
    Task<ActivityReport> GetAsync(
        ReportPeriodKind period,
        DateOnly today,
        CancellationToken cancellationToken = default);
}
