using KeyPulse.Core.Models;

namespace KeyPulse.Core.Interfaces;

public interface IPointerHeatmapQuery
{
    Task<PointerHeatmapResult?> GetAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}

public sealed record PointerClickPoint(string MonitorId, int X, int Y, long Count);

public sealed record PointerDensityGrid(
    string MonitorId,
    int Width,
    int Height,
    uint[] Cells);

public sealed record PointerCoverageGrid(
    string MonitorId,
    int Width,
    int Height,
    byte[] Cells);

public sealed record PointerHeatmapResult(
    DisplayLayout Layout,
    IReadOnlyList<PointerClickPoint> Clicks,
    IReadOnlyList<PointerDensityGrid> Densities,
    IReadOnlyList<PointerCoverageGrid> Coverages,
    long VisitedPixels,
    long TotalPixels);
