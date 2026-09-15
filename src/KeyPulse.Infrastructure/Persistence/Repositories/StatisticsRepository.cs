using System.Globalization;
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
                   mouse_distance_pixels
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
                    reader.GetDouble(10))));
        }

        return Task.FromResult<IReadOnlyList<DailyMouseRow>>(rows);
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
                   wheel_event_count, mouse_distance_pixels, active_seconds
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
                reader.GetInt64(6)));
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
        var distance = mouseRow?.Mouse.DistancePixels ?? 0;
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
                   s.active_seconds
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
                reader.GetInt64(7)));
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

    private static void UpsertMouse(SqliteConnection connection, DateOnly date, MouseTotals mouse)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO daily_mouse_stats (
                stat_date,
                left_click_count, right_click_count, middle_click_count,
                xbutton1_click_count, xbutton2_click_count,
                wheel_up_count, wheel_down_count, wheel_left_count, wheel_right_count,
                mouse_distance_pixels)
            VALUES (
                $date, $left, $right, $middle, $x1, $x2,
                $wheelUp, $wheelDown, $wheelLeft, $wheelRight, $distance)
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
                mouse_distance_pixels = mouse_distance_pixels + excluded.mouse_distance_pixels;
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
        command.ExecuteNonQuery();
    }

    private static void UpsertHourly(SqliteConnection connection, HourBucket bucket, HourlyActivity activity)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO hourly_activity_stats (
                stat_date, stat_hour,
                key_press_count, mouse_click_count, wheel_event_count,
                mouse_distance_pixels, active_seconds)
            VALUES ($date, $hour, $keys, $clicks, $wheels, $distance, 0)
            ON CONFLICT(stat_date, stat_hour) DO UPDATE SET
                key_press_count = key_press_count + excluded.key_press_count,
                mouse_click_count = mouse_click_count + excluded.mouse_click_count,
                wheel_event_count = wheel_event_count + excluded.wheel_event_count,
                mouse_distance_pixels = mouse_distance_pixels + excluded.mouse_distance_pixels,
                active_seconds = active_seconds + excluded.active_seconds;
            """;
        command.Parameters.AddWithValue("$date", Format(bucket.Date));
        command.Parameters.AddWithValue("$hour", bucket.Hour);
        command.Parameters.AddWithValue("$keys", activity.KeyPressCount);
        command.Parameters.AddWithValue("$clicks", activity.MouseClickCount);
        command.Parameters.AddWithValue("$wheels", activity.WheelEventCount);
        command.Parameters.AddWithValue("$distance", activity.MouseDistancePixels);
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
                mouse_distance_pixels, active_seconds)
            VALUES ($date, $app, $keys, $clicks, $wheels, $distance, $active)
            ON CONFLICT(stat_date, app_id) DO UPDATE SET
                key_press_count = key_press_count + excluded.key_press_count,
                mouse_click_count = mouse_click_count + excluded.mouse_click_count,
                wheel_event_count = wheel_event_count + excluded.wheel_event_count,
                mouse_distance_pixels = mouse_distance_pixels + excluded.mouse_distance_pixels,
                active_seconds = active_seconds + excluded.active_seconds;
            """;
        command.Parameters.AddWithValue("$date", Format(date));
        command.Parameters.AddWithValue("$app", appId);
        command.Parameters.AddWithValue("$keys", totals.KeyPressCount);
        command.Parameters.AddWithValue("$clicks", totals.MouseClickCount);
        command.Parameters.AddWithValue("$wheels", totals.WheelEventCount);
        command.Parameters.AddWithValue("$distance", totals.MouseDistancePixels);
        command.Parameters.AddWithValue("$active", totals.ActiveSeconds);
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

    private static string Format(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly ParseDate(string value) =>
        DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
