namespace KeyPulse.Core.Interfaces;

public interface IStatisticsExport
{
    Task<IReadOnlyList<string>> ExportCsvAsync(
        string directory,
        CancellationToken cancellationToken = default);
}