using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Input;
using Microsoft.Data.Sqlite;

namespace KeyPulse.Infrastructure.Persistence.Repositories;

public sealed class StatisticsRepository : IStatisticsRepository
{
    private readonly SqliteConnectionFactory _factory;

    public StatisticsRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public Task FlushAsync(StatisticsBatch batch, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (batch.IsEmpty)
        {
            return Task.CompletedTask;
        }

        using var connection = _factory.Open();
        using var begin = connection.CreateCommand();
        begin.CommandText = "BEGIN IMMEDIATE;";
        begin.ExecuteNonQuery();

        try
        {
            foreach (var day in batch.KeyCountsByDate)
            {
                foreach (var key in day.Value)
                {
                    UpsertKey(connection, day.Key, key.Key, key.Value);
                }
            }

            foreach (var day in batch.ShortcutCountsByDate)
            {
                foreach (var shortcut in day.Value)
                {
                    UpsertShortcut(connection, day.Key, shortcut.Key, shortcut.Value);
                }
            }

            foreach (var day in batch.MouseByDate)
            {
                UpsertMouse(connection, day.Key, day.Value);
            }

            foreach (var hour in batch.HourlyCounts)
            {
                UpsertHourly(connection, hour.Key, hour.Value);
            }

            var seenAt = DateTimeOffset.Now.ToString("o");
            foreach (var day in batch.AppStatsByDate)
            {
                foreach (var app in day.Value)
                {
                    var processName = ProcessNameGuard.Sanitize(app.Key);
                    if (processName is null)
                    {
                        continue;
                    }

                    var displayName = ProcessNameGuard.SanitizeDisplayName(app.Value.DisplayName);
                    var appId = GetOrCreateAppId(connection, processName, displayName, seenAt);
                    UpsertAppDay(connection, day.Key, appId, app.Value);
                }
            }

            if (batch.Pointer is { IsEmpty: false } pointer)
            {
                UpsertPointerStatistics(connection, pointer);
            }

            UpsertMeta(connection, "last_successful_flush", DateTimeOffset.Now.ToString("o"));

            using var commit = connection.CreateCommand();
            commit.CommandText = "COMMIT;";
            commit.ExecuteNonQuery();
        }
        catch
        {
            using var rollback = connection.CreateCommand();
            rollback.CommandText = "ROLLBACK;";
            rollback.ExecuteNonQuery();
            throw;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DailyKeyRow>> GetKeyStatsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT stat_date, key_code, press_count
            FROM daily_key_stats
            WHERE stat_date BETWEEN $from AND $to
            ORDER BY stat_date, key_code;
            """;
        command.Parameters.AddWithValue("$from", Format(from));
        command.Parameters.AddWithValue("$to", Format(to));

        var rows = new List<DailyKeyRow>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new DailyKeyRow(ParseDate(reader.GetString(0)), reader.GetString(1), reader.GetInt64(2)));
        }

        return Task.FromResult<IReadOnlyList<DailyKeyRow>>(rows);
    }

    public Task<IReadOnlyList<DailyMouseRow>> GetMouseStatsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT stat_date,
                   left_click_count, right_click_count, middle_click_count,
                   xbutton1_click_count, xbutton2_click_count,
                   wheel_up_count, wheel_down_count, wheel_left_count, wheel_right_count,
                   mouse_distance_pixels, cursor_distance_pixels, estimated_distance_meters
            FROM daily_mouse_stats
            WHERE stat_date BETWEEN $from AND $to
            ORDER BY stat_date;
            """;
        command.Parameters.AddWithValue("$from", Format(from));
        command.Parameters.AddWithValue("$to", Format(to));

        var rows = new List<DailyMouseRow>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new DailyMouseRow(
                ParseDate(reader.GetString(0)),
                new MouseTotals(
                    reader.GetInt64(1),
                    reader.GetInt64(2),
                    reader.GetInt64(3),
                    reader.GetInt64(4),
                    reader.GetInt64(5),
                    reader.GetInt64(6),
                    reader.GetInt64(7),
                    reader.GetInt64(8),
                    reader.GetInt64(9),
                    reader.GetDouble(10),
                    reader.GetDouble(11),
                    reader.GetDouble(12))));
        }

        return Task.FromResult<IReadOnlyList<DailyMouseRow>>(rows);
    }

    public Task<IReadOnlyList<DailyShortcutRow>> GetShortcutStatsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = _factory.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT stat_date, shortcut_code, press_count
            FROM daily_shortcut_stats
            WHERE stat_date BETWEEN $from AND $to
            ORDER BY stat_date, shortcut_code;
            """;
        command.Parameters.AddWithValue("$from", Format(from));
        command.Parameters.AddWithValue("$to", Format(to));

        var rows = new List<DailyShortcutRow>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new DailyShortcutRow(
                ParseDate(reader.GetString(0)), reader.GetString(1), reader.GetInt64(2)));
        }

        return Task.FromResult<IReadOnlyList<DailyShortcutRow>>(rows);
    }

    public Task<IReadOnlyList<HourlyRow>> GetHourlyAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT stat_date, stat_hour, key_press_count, mouse_click_count,
                   wheel_event_count, mouse_distance_pixels, active_seconds,
                   cursor_distance_pixels, estimated_distance_meters
            FROM hourly_activity_stats
            WHERE stat_date BETWEEN $from AND $to
            ORDER BY stat_date, stat_hour;
            """;
        command.Parameters.AddWithValue("$from", Format(from));
        command.Parameters.AddWithValue("$to", Format(to));

        var rows = new List<HourlyRow>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new HourlyRow(
                ParseDate(reader.GetString(0)),
                reader.GetInt32(1),
                reader.GetInt64(2),
                reader.GetInt64(3),
                reader.GetInt64(4),
                reader.GetDouble(5),
                reader.GetInt64(6),
                reader.GetDouble(7),
                reader.GetDouble(8)));
        }

        return Task.FromResult<IReadOnlyList<HourlyRow>>(rows);
    }

    public async Task<DashboardSummary> GetDashboardAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var keys = await GetKeyStatsAsync(date, date, cancellationToken);
        var mouse = await GetMouseStatsAsync(date, date, cancellationToken);
        var keyTotal = keys.Sum(row => row.PressCount);
        var mouseRow = mouse.FirstOrDefault();
        var clicks = mouseRow is null
            ? 0
            : mouseRow.Mouse.Left + mouseRow.Mouse.Right + mouseRow.Mouse.Middle +
              mouseRow.Mouse.XButton1 + mouseRow.Mouse.XButton2;
        var distance = mouseRow?.Mouse.CursorDistancePixels ?? 0;
        return new DashboardSummary(date, keyTotal, clicks, distance);
    }

    public async Task<IReadOnlyList<DailyKeyRow>> GetTrendAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var keys = await GetKeyStatsAsync(from, to, cancellationToken);
        return keys
            .GroupBy(row => row.Date)
            .Select(group => new DailyKeyRow(group.Key, "Total", group.Sum(row => row.PressCount)))
            .OrderBy(row => row.Date)
            .ToList();
    }

    public Task<IReadOnlyList<DailyAppRow>> GetAppStatsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = _factory.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.stat_date,
                   a.process_name,
                   a.display_name,
                   s.key_press_count,
                   s.mouse_click_count,
                   s.wheel_event_count,
                   s.mouse_distance_pixels,
                   s.active_seconds,
                   s.cursor_distance_pixels,
                   s.estimated_distance_meters
            FROM daily_app_stats s
            JOIN app_registry a ON a.app_id = s.app_id
            WHERE s.stat_date BETWEEN $from AND $to
            ORDER BY s.stat_date, a.process_name;
            """;
        command.Parameters.AddWithValue("$from", Format(from));
        command.Parameters.AddWithValue("$to", Format(to));

        var rows = new List<DailyAppRow>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new DailyAppRow(
                ParseDate(reader.GetString(0)),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetInt64(3),
                reader.GetInt64(4),
                reader.GetInt64(5),
                reader.GetDouble(6),
                reader.GetInt64(7),
                reader.GetDouble(8),
                reader.GetDouble(9)));
        }

        return Task.FromResult<IReadOnlyList<DailyAppRow>>(rows);
    }

    public Task<DateOnly?> GetEarliestStatDateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = _factory.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT MIN(stat_date) FROM (
                SELECT stat_date FROM daily_key_stats
                UNION ALL
                SELECT stat_date FROM daily_mouse_stats
                UNION ALL
                SELECT stat_date FROM hourly_activity_stats
                UNION ALL
                SELECT stat_date FROM daily_app_stats
            );
            """;
        var value = command.ExecuteScalar();
        if (value is null or DBNull)
        {
            return Task.FromResult<DateOnly?>(null);
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult<DateOnly?>(null);
        }

        return Task.FromResult<DateOnly?>(ParseDate(text));
    }

    public Task ClearStatisticsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = _factory.Open();
        using var begin = connection.CreateCommand();
        begin.CommandText = "BEGIN IMMEDIATE;";
        begin.ExecuteNonQuery();

        try
        {
            Execute(connection, "DELETE FROM daily_key_stats;");
            Execute(connection, "DELETE FROM daily_shortcut_stats;");
            Execute(connection, "DELETE FROM daily_mouse_stats;");
            Execute(connection, "DELETE FROM hourly_activity_stats;");
            Execute(connection, "DELETE FROM daily_app_stats;");
            Execute(connection, "DELETE FROM app_registry;");
            Execute(connection, "DELETE FROM hourly_click_points;");
            Execute(connection, "DELETE FROM daily_pointer_density;");
            Execute(connection, "DELETE FROM pointer_occupancy_tiles;");
            Execute(connection, "DELETE FROM display_monitors;");
            Execute(connection, "DELETE FROM display_layouts;");

            using var commit = connection.CreateCommand();
            commit.CommandText = "COMMIT;";
            commit.ExecuteNonQuery();
        }
        catch
        {
            using var rollback = connection.CreateCommand();
            rollback.CommandText = "ROLLBACK;";
            rollback.ExecuteNonQuery();
            throw;
        }

        return Task.CompletedTask;
    }

    public Task ClearStatisticsRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (to < from) (from, to) = (to, from);
        using var connection = _factory.Open();
        using var transaction = connection.BeginTransaction();
        foreach (var table in new[]
                 {
                     "daily_key_stats", "daily_shortcut_stats", "daily_mouse_stats",
                     "hourly_activity_stats", "daily_app_stats", "hourly_click_points", "daily_pointer_density"
                 })
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"DELETE FROM {table} WHERE stat_date BETWEEN $from AND $to;";
            command.Parameters.AddWithValue("$from", Format(from));
            command.Parameters.AddWithValue("$to", Format(to));
            command.ExecuteNonQuery();
        }

        using (var orphanApps = connection.CreateCommand())
        {
            orphanApps.Transaction = transaction;
            orphanApps.CommandText = "DELETE FROM app_registry WHERE app_id NOT IN (SELECT DISTINCT app_id FROM daily_app_stats);";
            orphanApps.ExecuteNonQuery();
        }
        transaction.Commit();
        return Task.CompletedTask;
    }

    public Task ClearPositionDataAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = _factory.Open();
        using var begin = connection.CreateCommand();
        begin.CommandText = "BEGIN IMMEDIATE;";
        begin.ExecuteNonQuery();
        try
        {
            Execute(connection, "DELETE FROM hourly_click_points;");
            Execute(connection, "DELETE FROM daily_pointer_density;");
            Execute(connection, "DELETE FROM pointer_occupancy_tiles;");
            Execute(connection, "DELETE FROM display_monitors;");
            Execute(connection, "DELETE FROM display_layouts;");
            Execute(connection, "COMMIT;");
        }
        catch
        {
            Execute(connection, "ROLLBACK;");
            throw;
        }
        return Task.CompletedTask;
    }

    public Task PrunePositionDataAsync(DateOnly beforeDate, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = _factory.Open();
        using var transaction = connection.BeginTransaction();
        foreach (var table in new[] { "hourly_click_points", "daily_pointer_density" })
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"DELETE FROM {table} WHERE stat_date < $before;";
            command.Parameters.AddWithValue("$before", Format(beforeDate));
            command.ExecuteNonQuery();
        }

        transaction.Commit();
        return Task.CompletedTask;
    }

    public Task ClearOccupancyDataAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = _factory.Open();
        Execute(connection, "DELETE FROM pointer_occupancy_tiles;");
        return Task.CompletedTask;
    }

    public bool TryPing()
    {
        try
        {
            using var connection = _factory.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            command.ExecuteScalar();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void UpsertKey(SqliteConnection connection, DateOnly date, string key, long count)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO daily_key_stats (stat_date, key_code, press_count)
            VALUES ($date, $key, $count)
            ON CONFLICT(stat_date, key_code)
            DO UPDATE SET press_count = press_count + excluded.press_count;
            """;
        command.Parameters.AddWithValue("$date", Format(date));
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$count", count);
        command.ExecuteNonQuery();
    }

    private static void UpsertShortcut(
        SqliteConnection connection,
        DateOnly date,
        string shortcut,
        long count)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO daily_shortcut_stats (stat_date, shortcut_code, press_count)
            VALUES ($date, $shortcut, $count)
            ON CONFLICT(stat_date, shortcut_code)
            DO UPDATE SET press_count = press_count + excluded.press_count;
            """;
        command.Parameters.AddWithValue("$date", Format(date));
        command.Parameters.AddWithValue("$shortcut", shortcut);
        command.Parameters.AddWithValue("$count", count);
        command.ExecuteNonQuery();
    }

    private static void UpsertMouse(SqliteConnection connection, DateOnly date, MouseTotals mouse)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO daily_mouse_stats (
                stat_date,
                left_click_count, right_click_count, middle_click_count,
                xbutton1_click_count, xbutton2_click_count,
                wheel_up_count, wheel_down_count, wheel_left_count, wheel_right_count,
                mouse_distance_pixels, cursor_distance_pixels, estimated_distance_meters)
            VALUES (
                $date, $left, $right, $middle, $x1, $x2,
                $wheelUp, $wheelDown, $wheelLeft, $wheelRight, $distance, $cursorDistance, $meters)
            ON CONFLICT(stat_date) DO UPDATE SET
                left_click_count = left_click_count + excluded.left_click_count,
                right_click_count = right_click_count + excluded.right_click_count,
                middle_click_count = middle_click_count + excluded.middle_click_count,
                xbutton1_click_count = xbutton1_click_count + excluded.xbutton1_click_count,
                xbutton2_click_count = xbutton2_click_count + excluded.xbutton2_click_count,
                wheel_up_count = wheel_up_count + excluded.wheel_up_count,
                wheel_down_count = wheel_down_count + excluded.wheel_down_count,
                wheel_left_count = wheel_left_count + excluded.wheel_left_count,
                wheel_right_count = wheel_right_count + excluded.wheel_right_count,
                mouse_distance_pixels = mouse_distance_pixels + excluded.mouse_distance_pixels,
                cursor_distance_pixels = cursor_distance_pixels + excluded.cursor_distance_pixels,
                estimated_distance_meters = estimated_distance_meters + excluded.estimated_distance_meters;
            """;
        command.Parameters.AddWithValue("$date", Format(date));
        command.Parameters.AddWithValue("$left", mouse.Left);
        command.Parameters.AddWithValue("$right", mouse.Right);
        command.Parameters.AddWithValue("$middle", mouse.Middle);
        command.Parameters.AddWithValue("$x1", mouse.XButton1);
        command.Parameters.AddWithValue("$x2", mouse.XButton2);
        command.Parameters.AddWithValue("$wheelUp", mouse.WheelUp);
        command.Parameters.AddWithValue("$wheelDown", mouse.WheelDown);
        command.Parameters.AddWithValue("$wheelLeft", mouse.WheelLeft);
        command.Parameters.AddWithValue("$wheelRight", mouse.WheelRight);
        command.Parameters.AddWithValue("$distance", mouse.DistancePixels);
        command.Parameters.AddWithValue("$cursorDistance", mouse.CursorDistancePixels);
        command.Parameters.AddWithValue("$meters", mouse.EstimatedDistanceMeters);
        command.ExecuteNonQuery();
    }

    private static void UpsertHourly(SqliteConnection connection, HourBucket bucket, HourlyActivity activity)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO hourly_activity_stats (
                stat_date, stat_hour,
                key_press_count, mouse_click_count, wheel_event_count,
                mouse_distance_pixels, active_seconds, cursor_distance_pixels, estimated_distance_meters)
            VALUES ($date, $hour, $keys, $clicks, $wheels, $distance, 0, $cursorDistance, $meters)
            ON CONFLICT(stat_date, stat_hour) DO UPDATE SET
                key_press_count = key_press_count + excluded.key_press_count,
                mouse_click_count = mouse_click_count + excluded.mouse_click_count,
                wheel_event_count = wheel_event_count + excluded.wheel_event_count,
                mouse_distance_pixels = mouse_distance_pixels + excluded.mouse_distance_pixels,
                active_seconds = active_seconds + excluded.active_seconds,
                cursor_distance_pixels = cursor_distance_pixels + excluded.cursor_distance_pixels,
                estimated_distance_meters = estimated_distance_meters + excluded.estimated_distance_meters;
            """;
        command.Parameters.AddWithValue("$date", Format(bucket.Date));
        command.Parameters.AddWithValue("$hour", bucket.Hour);
        command.Parameters.AddWithValue("$keys", activity.KeyPressCount);
        command.Parameters.AddWithValue("$clicks", activity.MouseClickCount);
        command.Parameters.AddWithValue("$wheels", activity.WheelEventCount);
        command.Parameters.AddWithValue("$distance", activity.MouseDistancePixels);
        command.Parameters.AddWithValue("$cursorDistance", activity.CursorDistancePixels);
        command.Parameters.AddWithValue("$meters", activity.EstimatedDistanceMeters);
        command.ExecuteNonQuery();
    }

    private static long GetOrCreateAppId(
        SqliteConnection connection,
        string processName,
        string? displayName,
        string seenAt)
    {
        using (var find = connection.CreateCommand())
        {
            find.CommandText = """
                SELECT app_id FROM app_registry
                WHERE process_name = $name COLLATE NOCASE;
                """;
            find.Parameters.AddWithValue("$name", processName);
            var existing = find.ExecuteScalar();
            if (existing is not null and not DBNull)
            {
                var id = Convert.ToInt64(existing, CultureInfo.InvariantCulture);
                UpdateRegistry(connection, id, displayName, seenAt);
                return id;
            }
        }

        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO app_registry (process_name, display_name, first_seen_at, last_seen_at)
            VALUES ($name, $display, $seen, $seen);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$name", processName);
        insert.Parameters.AddWithValue("$display", (object?)displayName ?? DBNull.Value);
        insert.Parameters.AddWithValue("$seen", seenAt);
        return Convert.ToInt64(insert.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void UpdateRegistry(
        SqliteConnection connection,
        long appId,
        string? displayName,
        string seenAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE app_registry
            SET last_seen_at = $seen,
                display_name = CASE
                    WHEN $display IS NOT NULL THEN $display
                    ELSE display_name
                END
            WHERE app_id = $id;
            """;
        command.Parameters.AddWithValue("$seen", seenAt);
        command.Parameters.AddWithValue("$display", (object?)displayName ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", appId);
        command.ExecuteNonQuery();
    }

    private static void UpsertAppDay(
        SqliteConnection connection,
        DateOnly date,
        long appId,
        AppDayTotals totals)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO daily_app_stats (
                stat_date, app_id,
                key_press_count, mouse_click_count, wheel_event_count,
                mouse_distance_pixels, active_seconds, cursor_distance_pixels, estimated_distance_meters)
            VALUES ($date, $app, $keys, $clicks, $wheels, $distance, $active, $cursorDistance, $meters)
            ON CONFLICT(stat_date, app_id) DO UPDATE SET
                key_press_count = key_press_count + excluded.key_press_count,
                mouse_click_count = mouse_click_count + excluded.mouse_click_count,
                wheel_event_count = wheel_event_count + excluded.wheel_event_count,
                mouse_distance_pixels = mouse_distance_pixels + excluded.mouse_distance_pixels,
                active_seconds = active_seconds + excluded.active_seconds,
                cursor_distance_pixels = cursor_distance_pixels + excluded.cursor_distance_pixels,
                estimated_distance_meters = estimated_distance_meters + excluded.estimated_distance_meters;
            """;
        command.Parameters.AddWithValue("$date", Format(date));
        command.Parameters.AddWithValue("$app", appId);
        command.Parameters.AddWithValue("$keys", totals.KeyPressCount);
        command.Parameters.AddWithValue("$clicks", totals.MouseClickCount);
        command.Parameters.AddWithValue("$wheels", totals.WheelEventCount);
        command.Parameters.AddWithValue("$distance", totals.MouseDistancePixels);
        command.Parameters.AddWithValue("$active", totals.ActiveSeconds);
        command.Parameters.AddWithValue("$cursorDistance", totals.CursorDistancePixels);
        command.Parameters.AddWithValue("$meters", totals.EstimatedDistanceMeters);
        command.ExecuteNonQuery();
    }

    private static void UpsertMeta(SqliteConnection connection, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO settings_meta (meta_key, meta_value)
            VALUES ($key, $value)
            ON CONFLICT(meta_key) DO UPDATE SET meta_value = excluded.meta_value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private static void UpsertPointerStatistics(SqliteConnection connection, PointerStatistics pointer)
    {
        var layoutIds = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var layout in pointer.Layouts.Values)
        {
            layoutIds[layout.Signature] = UpsertDisplayLayout(connection, layout);
        }

        foreach (var click in pointer.Clicks)
        {
            if (!layoutIds.TryGetValue(click.Key.LayoutSignature, out var layoutId)) continue;
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO hourly_click_points (
                    stat_date, stat_hour, layout_id, monitor_id, x_px, y_px, button_code, click_count)
                VALUES ($date, $hour, $layout, $monitor, $x, $y, $button, $count)
                ON CONFLICT(stat_date, stat_hour, layout_id, monitor_id, x_px, y_px, button_code)
                DO UPDATE SET click_count = click_count + excluded.click_count;
                """;
            command.Parameters.AddWithValue("$date", Format(click.Key.Date));
            command.Parameters.AddWithValue("$hour", click.Key.Hour);
            command.Parameters.AddWithValue("$layout", layoutId);
            command.Parameters.AddWithValue("$monitor", click.Key.MonitorId);
            command.Parameters.AddWithValue("$x", click.Key.X);
            command.Parameters.AddWithValue("$y", click.Key.Y);
            command.Parameters.AddWithValue("$button", click.Key.ButtonCode);
            command.Parameters.AddWithValue("$count", click.Value);
            command.ExecuteNonQuery();
        }

        foreach (var density in pointer.Densities)
        {
            if (!layoutIds.TryGetValue(density.Key.LayoutSignature, out var layoutId)) continue;
            UpsertDensity(connection, density.Key, density.Value, layoutId);
        }

        foreach (var tile in pointer.OccupancyTiles)
        {
            if (!layoutIds.TryGetValue(tile.Key.LayoutSignature, out var layoutId)) continue;
            UpsertOccupancyTile(connection, tile.Key, tile.Value, layoutId);
        }
    }

    private static long UpsertDisplayLayout(SqliteConnection connection, KeyPulse.Core.Models.DisplayLayout layout)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO display_layouts (
                    layout_signature, virtual_left, virtual_top, virtual_width, virtual_height, created_at)
                VALUES ($signature, $left, $top, $width, $height, $created)
                ON CONFLICT(layout_signature) DO NOTHING;
                """;
            command.Parameters.AddWithValue("$signature", layout.Signature);
            command.Parameters.AddWithValue("$left", layout.VirtualLeft);
            command.Parameters.AddWithValue("$top", layout.VirtualTop);
            command.Parameters.AddWithValue("$width", layout.VirtualWidth);
            command.Parameters.AddWithValue("$height", layout.VirtualHeight);
            command.Parameters.AddWithValue("$created", DateTimeOffset.Now.ToString("o"));
            command.ExecuteNonQuery();
        }

        long layoutId;
        using (var find = connection.CreateCommand())
        {
            find.CommandText = "SELECT layout_id FROM display_layouts WHERE layout_signature = $signature;";
            find.Parameters.AddWithValue("$signature", layout.Signature);
            layoutId = Convert.ToInt64(find.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        foreach (var monitor in layout.Monitors)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO display_monitors (
                    layout_id, monitor_id, left_px, top_px, width_px, height_px, dpi_x, dpi_y, is_primary)
                VALUES ($layout, $monitor, $left, $top, $width, $height, $dpiX, $dpiY, $primary)
                ON CONFLICT(layout_id, monitor_id) DO UPDATE SET
                    left_px = excluded.left_px, top_px = excluded.top_px,
                    width_px = excluded.width_px, height_px = excluded.height_px,
                    dpi_x = excluded.dpi_x, dpi_y = excluded.dpi_y,
                    is_primary = excluded.is_primary;
                """;
            command.Parameters.AddWithValue("$layout", layoutId);
            command.Parameters.AddWithValue("$monitor", monitor.Id);
            command.Parameters.AddWithValue("$left", monitor.Left);
            command.Parameters.AddWithValue("$top", monitor.Top);
            command.Parameters.AddWithValue("$width", monitor.Width);
            command.Parameters.AddWithValue("$height", monitor.Height);
            command.Parameters.AddWithValue("$dpiX", monitor.DpiX);
            command.Parameters.AddWithValue("$dpiY", monitor.DpiY);
            command.Parameters.AddWithValue("$primary", monitor.IsPrimary ? 1 : 0);
            command.ExecuteNonQuery();
        }

        return layoutId;
    }

    private static void UpsertDensity(
        SqliteConnection connection,
        PointerDensityKey key,
        PointerDensityData incoming,
        long layoutId)
    {
        var cells = (uint[])incoming.Cells.Clone();
        var samples = incoming.SampleCount;
        using (var read = connection.CreateCommand())
        {
            read.CommandText = """
                SELECT grid_width, grid_height, sample_count, density_blob
                FROM daily_pointer_density
                WHERE stat_date = $date AND layout_id = $layout AND monitor_id = $monitor;
                """;
            read.Parameters.AddWithValue("$date", Format(key.Date));
            read.Parameters.AddWithValue("$layout", layoutId);
            read.Parameters.AddWithValue("$monitor", key.MonitorId);
            using var reader = read.ExecuteReader();
            if (reader.Read() && reader.GetInt32(0) == incoming.Width && reader.GetInt32(1) == incoming.Height)
            {
                var existing = BytesToUInts((byte[])reader[3]);
                for (var i = 0; i < Math.Min(existing.Length, cells.Length); i++)
                    cells[i] = (uint)Math.Min(uint.MaxValue, (ulong)cells[i] + existing[i]);
                samples += reader.GetInt64(2);
            }
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO daily_pointer_density (
                stat_date, layout_id, monitor_id, grid_width, grid_height,
                sample_count, format_version, compression_code, density_blob)
            VALUES ($date, $layout, $monitor, $width, $height, $samples, 1, 'none', $blob)
            ON CONFLICT(stat_date, layout_id, monitor_id) DO UPDATE SET
                grid_width = excluded.grid_width, grid_height = excluded.grid_height,
                sample_count = excluded.sample_count, format_version = excluded.format_version,
                compression_code = excluded.compression_code, density_blob = excluded.density_blob;
            """;
        command.Parameters.AddWithValue("$date", Format(key.Date));
        command.Parameters.AddWithValue("$layout", layoutId);
        command.Parameters.AddWithValue("$monitor", key.MonitorId);
        command.Parameters.AddWithValue("$width", incoming.Width);
        command.Parameters.AddWithValue("$height", incoming.Height);
        command.Parameters.AddWithValue("$samples", samples);
        command.Parameters.AddWithValue("$blob", UIntsToBytes(cells));
        command.ExecuteNonQuery();
    }

    private static void UpsertOccupancyTile(
        SqliteConnection connection,
        OccupancyTileKey key,
        OccupancyTileData incoming,
        long layoutId)
    {
        var bits = (byte[])incoming.Bits.Clone();
        using (var read = connection.CreateCommand())
        {
            read.CommandText = """
                SELECT bits_blob FROM pointer_occupancy_tiles
                WHERE layout_id = $layout AND monitor_id = $monitor AND tile_x = $x AND tile_y = $y;
                """;
            read.Parameters.AddWithValue("$layout", layoutId);
            read.Parameters.AddWithValue("$monitor", key.MonitorId);
            read.Parameters.AddWithValue("$x", key.TileX);
            read.Parameters.AddWithValue("$y", key.TileY);
            var existing = read.ExecuteScalar() as byte[];
            if (existing is not null)
                for (var i = 0; i < Math.Min(bits.Length, existing.Length); i++) bits[i] |= existing[i];
        }

        var visited = bits.Sum(value => BitOperations.PopCount((uint)value));
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO pointer_occupancy_tiles (
                layout_id, monitor_id, tile_x, tile_y, tile_width, tile_height,
                visited_count, format_version, bits_blob)
            VALUES ($layout, $monitor, $x, $y, $width, $height, $visited, 1, $bits)
            ON CONFLICT(layout_id, monitor_id, tile_x, tile_y) DO UPDATE SET
                tile_width = excluded.tile_width, tile_height = excluded.tile_height,
                visited_count = excluded.visited_count, format_version = excluded.format_version,
                bits_blob = excluded.bits_blob;
            """;
        command.Parameters.AddWithValue("$layout", layoutId);
        command.Parameters.AddWithValue("$monitor", key.MonitorId);
        command.Parameters.AddWithValue("$x", key.TileX);
        command.Parameters.AddWithValue("$y", key.TileY);
        command.Parameters.AddWithValue("$width", incoming.Width);
        command.Parameters.AddWithValue("$height", incoming.Height);
        command.Parameters.AddWithValue("$visited", visited);
        command.Parameters.AddWithValue("$bits", bits);
        command.ExecuteNonQuery();
    }

    private static byte[] UIntsToBytes(uint[] values)
    {
        var bytes = new byte[values.Length * sizeof(uint)];
        MemoryMarshal.AsBytes(values.AsSpan()).CopyTo(bytes);
        return bytes;
    }

    private static uint[] BytesToUInts(byte[] bytes)
    {
        var values = new uint[bytes.Length / sizeof(uint)];
        bytes.AsSpan().CopyTo(MemoryMarshal.AsBytes(values.AsSpan()));
        return values;
    }

    private static string Format(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly ParseDate(string value) =>
        DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
