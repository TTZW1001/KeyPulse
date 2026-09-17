using System.Runtime.InteropServices;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Models;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Persistence;

namespace KeyPulse.Infrastructure.Query;

public sealed class PointerHeatmapQueryService : IPointerHeatmapQuery
{
    private readonly SqliteConnectionFactory _factory;
    private readonly IStatisticsReader _reader;

    public PointerHeatmapQueryService(SqliteConnectionFactory factory, IStatisticsReader reader)
    {
        _factory = factory;
        _reader = reader;
    }

    public Task<PointerHeatmapResult?> GetAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (to < from) (from, to) = (to, from);
        cancellationToken.ThrowIfCancellationRequested();
        var unflushed = _reader.CaptureUnflushed().Pointer;
        using var connection = _factory.Open();

        var signature = FindLayoutSignature(connection, from, to)
                        ?? unflushed?.Layouts.Keys.LastOrDefault();
        if (signature is null) return Task.FromResult<PointerHeatmapResult?>(null);

        var layout = ReadLayout(connection, signature)
                     ?? (unflushed?.Layouts.TryGetValue(signature, out var liveLayout) == true ? liveLayout : null);
        if (layout is null) return Task.FromResult<PointerHeatmapResult?>(null);

        var clicks = ReadClicks(connection, signature, from, to);
        var densities = ReadDensities(connection, signature, from, to);
        var occupancy = ReadOccupancy(connection, signature);

        if (unflushed is not null)
        {
            MergeUnflushed(unflushed, signature, from, to, clicks, densities, occupancy);
        }

        var visited = occupancy.Values.Sum(tile => (long)CountBits(tile.Bits));
        var coverages = BuildCoverage(layout, occupancy);
        var total = layout.Monitors.Sum(monitor => (long)monitor.Width * monitor.Height);
        return Task.FromResult<PointerHeatmapResult?>(new PointerHeatmapResult(
            layout,
            clicks.Select(pair => new PointerClickPoint(
                pair.Key.MonitorId, pair.Key.X, pair.Key.Y, pair.Value, pair.Key.ButtonCode)).ToList(),
            densities.Select(pair => new PointerDensityGrid(
                pair.Key, pair.Value.Width, pair.Value.Height, pair.Value.Cells)).ToList(),
            coverages,
            Math.Min(visited, total), total));
    }

    private static string? FindLayoutSignature(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        DateOnly from,
        DateOnly to)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT l.layout_signature
            FROM display_layouts l
            ORDER BY (
                COALESCE((SELECT SUM(d.sample_count) FROM daily_pointer_density d
                          WHERE d.layout_id = l.layout_id AND d.stat_date BETWEEN $from AND $to), 0)
                +
                COALESCE((SELECT SUM(c.click_count) FROM hourly_click_points c
                          WHERE c.layout_id = l.layout_id AND c.stat_date BETWEEN $from AND $to), 0)
                     ) DESC,
                     l.layout_id DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));
        return command.ExecuteScalar() as string;
    }

    private static DisplayLayout? ReadLayout(Microsoft.Data.Sqlite.SqliteConnection connection, string signature)
    {
        long id;
        int left, top, width, height;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT layout_id, virtual_left, virtual_top, virtual_width, virtual_height
                FROM display_layouts WHERE layout_signature = $signature;
                """;
            command.Parameters.AddWithValue("$signature", signature);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return null;
            id = reader.GetInt64(0); left = reader.GetInt32(1); top = reader.GetInt32(2);
            width = reader.GetInt32(3); height = reader.GetInt32(4);
        }

        var monitors = new List<DisplayMonitor>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT monitor_id, left_px, top_px, width_px, height_px, dpi_x, dpi_y, is_primary
                FROM display_monitors WHERE layout_id = $layout ORDER BY left_px, top_px;
                """;
            command.Parameters.AddWithValue("$layout", id);
            using var reader = command.ExecuteReader();
            while (reader.Read())
                monitors.Add(new DisplayMonitor(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2),
                    reader.GetInt32(3), reader.GetInt32(4), reader.GetDouble(5), reader.GetDouble(6),
                    reader.GetInt32(7) != 0));
        }
        return new DisplayLayout(signature, left, top, width, height, monitors);
    }

    private static Dictionary<(string MonitorId, int X, int Y, string ButtonCode), long> ReadClicks(
        Microsoft.Data.Sqlite.SqliteConnection connection, string signature, DateOnly from, DateOnly to)
    {
        var result = new Dictionary<(string, int, int, string), long>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.monitor_id, c.x_px, c.y_px, c.button_code, SUM(c.click_count)
            FROM hourly_click_points c
            JOIN display_layouts l ON l.layout_id = c.layout_id
            WHERE l.layout_signature = $signature AND c.stat_date BETWEEN $from AND $to
            GROUP BY c.monitor_id, c.x_px, c.y_px, c.button_code;
            """;
        command.Parameters.AddWithValue("$signature", signature);
        command.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));
        using var reader = command.ExecuteReader();
        while (reader.Read()) result[(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3))] = reader.GetInt64(4);
        return result;
    }

    private static Dictionary<string, PointerDensityData> ReadDensities(
        Microsoft.Data.Sqlite.SqliteConnection connection, string signature, DateOnly from, DateOnly to)
    {
        var result = new Dictionary<string, PointerDensityData>(StringComparer.Ordinal);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.monitor_id, d.grid_width, d.grid_height, d.sample_count, d.density_blob
            FROM daily_pointer_density d
            JOIN display_layouts l ON l.layout_id = d.layout_id
            WHERE l.layout_signature = $signature AND d.stat_date BETWEEN $from AND $to;
            """;
        command.Parameters.AddWithValue("$signature", signature);
        command.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var monitor = reader.GetString(0);
            var width = reader.GetInt32(1);
            var height = reader.GetInt32(2);
            var cells = BytesToUInts((byte[])reader[4]);
            if (!result.TryGetValue(monitor, out var existing))
                result[monitor] = new PointerDensityData(width, height, reader.GetInt64(3), cells);
            else
            {
                for (var i = 0; i < Math.Min(cells.Length, existing.Cells.Length); i++)
                    existing.Cells[i] = (uint)Math.Min(uint.MaxValue, (ulong)existing.Cells[i] + cells[i]);
            }
        }
        return result;
    }

    private static Dictionary<(string MonitorId, int TileX, int TileY), OccupancyTileData> ReadOccupancy(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string signature)
    {
        var tiles = new Dictionary<(string, int, int), OccupancyTileData>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.monitor_id, t.tile_x, t.tile_y, t.tile_width, t.tile_height, t.bits_blob
            FROM pointer_occupancy_tiles t
            JOIN display_layouts l ON l.layout_id = t.layout_id
            WHERE l.layout_signature = $signature;
            """;
        command.Parameters.AddWithValue("$signature", signature);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tiles[(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2))] = new OccupancyTileData(
                reader.GetInt32(3), reader.GetInt32(4), (byte[])reader[5]);
        }
        return tiles;
    }

    private static IReadOnlyList<PointerCoverageGrid> BuildCoverage(
        DisplayLayout layout,
        IReadOnlyDictionary<(string MonitorId, int TileX, int TileY), OccupancyTileData> tiles)
    {
        var grids = layout.Monitors.ToDictionary(
            monitor => monitor.Id,
            monitor => new CoverageBuilder(monitor), StringComparer.Ordinal);
        foreach (var tile in tiles)
        {
            if (grids.TryGetValue(tile.Key.MonitorId, out var grid))
            {
                grid.AddTile(tile.Key.TileX, tile.Key.TileY,
                    tile.Value.Width, tile.Value.Height, tile.Value.Bits);
            }
        }
        return grids.Select(pair => pair.Value.Build(pair.Key)).ToList();
    }

    private static void MergeUnflushed(
        PointerStatistics live, string signature, DateOnly from, DateOnly to,
        Dictionary<(string MonitorId, int X, int Y, string ButtonCode), long> clicks,
        Dictionary<string, PointerDensityData> densities,
        Dictionary<(string MonitorId, int TileX, int TileY), OccupancyTileData> occupancy)
    {
        foreach (var click in live.Clicks)
        {
            if (click.Key.LayoutSignature != signature || click.Key.Date < from || click.Key.Date > to) continue;
            var key = (click.Key.MonitorId, click.Key.X, click.Key.Y, click.Key.ButtonCode);
            clicks[key] = clicks.GetValueOrDefault(key) + click.Value;
        }
        foreach (var density in live.Densities)
        {
            if (density.Key.LayoutSignature != signature || density.Key.Date < from || density.Key.Date > to) continue;
            if (!densities.TryGetValue(density.Key.MonitorId, out var target))
                densities[density.Key.MonitorId] = density.Value with { Cells = (uint[])density.Value.Cells.Clone() };
            else
                for (var i = 0; i < Math.Min(target.Cells.Length, density.Value.Cells.Length); i++)
                    target.Cells[i] = (uint)Math.Min(uint.MaxValue, (ulong)target.Cells[i] + density.Value.Cells[i]);
        }
        foreach (var tile in live.OccupancyTiles.Where(pair => pair.Key.LayoutSignature == signature))
        {
            var key = (tile.Key.MonitorId, tile.Key.TileX, tile.Key.TileY);
            if (!occupancy.TryGetValue(key, out var target))
            {
                occupancy[key] = tile.Value with { Bits = (byte[])tile.Value.Bits.Clone() };
                continue;
            }

            for (var i = 0; i < Math.Min(target.Bits.Length, tile.Value.Bits.Length); i++)
            {
                target.Bits[i] |= tile.Value.Bits[i];
            }
        }
    }

    private static uint[] BytesToUInts(byte[] bytes)
    {
        var values = new uint[bytes.Length / sizeof(uint)];
        bytes.AsSpan().CopyTo(MemoryMarshal.AsBytes(values.AsSpan()));
        return values;
    }

    private static int CountBits(byte[] bits) => bits.Sum(value => global::System.Numerics.BitOperations.PopCount((uint)value));

    private sealed class CoverageBuilder
    {
        private const int GridWidth = 256;
        private readonly DisplayMonitor _monitor;
        private readonly int _height;
        private readonly int[] _visited;
        public CoverageBuilder(DisplayMonitor monitor)
        {
            _monitor = monitor;
            _height = Math.Max(1, (int)Math.Round(GridWidth * monitor.Height / (double)monitor.Width));
            _visited = new int[GridWidth * _height];
        }
        public void AddTile(int tileX, int tileY, int width, int height, byte[] bits)
        {
            for (var localY = 0; localY < height; localY++)
            for (var localX = 0; localX < width; localX++)
            {
                var bit = (localY * width) + localX;
                if ((bits[bit >> 3] & (1 << (bit & 7))) == 0) continue;
                var x = (tileX * 256) + localX;
                var y = (tileY * 256) + localY;
                var gx = Math.Min(GridWidth - 1, x * GridWidth / _monitor.Width);
                var gy = Math.Min(_height - 1, y * _height / _monitor.Height);
                _visited[(gy * GridWidth) + gx]++;
            }
        }
        public PointerCoverageGrid Build(string monitorId)
        {
            var cells = new byte[_visited.Length];
            var pixelsPerCell = Math.Max(1, (_monitor.Width * _monitor.Height) / cells.Length);
            for (var i = 0; i < cells.Length; i++)
                cells[i] = (byte)Math.Min(255, _visited[i] * 255 / pixelsPerCell);
            return new PointerCoverageGrid(monitorId, GridWidth, _height, cells);
        }
    }
}
