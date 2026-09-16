using KeyPulse.Core.Models;

namespace KeyPulse.Core.Statistics;

public readonly record struct ClickPointKey(
    DateOnly Date,
    int Hour,
    string LayoutSignature,
    string MonitorId,
    int X,
    int Y,
    string ButtonCode);

public readonly record struct PointerDensityKey(
    DateOnly Date,
    string LayoutSignature,
    string MonitorId);

public sealed record PointerDensityData(
    int Width,
    int Height,
    long SampleCount,
    uint[] Cells);

public readonly record struct OccupancyTileKey(
    string LayoutSignature,
    string MonitorId,
    int TileX,
    int TileY);

public sealed record OccupancyTileData(
    int Width,
    int Height,
    byte[] Bits);

public sealed record PointerStatistics(
    IReadOnlyDictionary<string, DisplayLayout> Layouts,
    IReadOnlyDictionary<ClickPointKey, long> Clicks,
    IReadOnlyDictionary<PointerDensityKey, PointerDensityData> Densities,
    IReadOnlyDictionary<OccupancyTileKey, OccupancyTileData> OccupancyTiles)
{
    public static PointerStatistics Empty { get; } = new(
        new Dictionary<string, DisplayLayout>(),
        new Dictionary<ClickPointKey, long>(),
        new Dictionary<PointerDensityKey, PointerDensityData>(),
        new Dictionary<OccupancyTileKey, OccupancyTileData>());

    public bool IsEmpty => Clicks.Count == 0 && Densities.Count == 0 && OccupancyTiles.Count == 0;
}
