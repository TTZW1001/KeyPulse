using System.Globalization;
using System.Text;
using KeyPulse.Core.Interfaces;
using KeyPulse.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace KeyPulse.Infrastructure.Query;

public sealed class StatisticsExportService : IStatisticsExport
{
    private static readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);

    private readonly SqliteConnectionFactory _factory;

    public StatisticsExportService(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public Task<IReadOnlyList<string>> ExportCsvAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Export directory is required.", nameof(directory));
        }

        Directory.CreateDirectory(directory);
        var stamp = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var written = new List<string>();

        using var connection = _factory.Open();
        written.Add(Write(
            directory,
            "keypulse-daily-shortcut-stats-" + stamp + ".csv",
            "stat_date,shortcut_code,press_count",
            connection,
            """
            SELECT stat_date, shortcut_code, press_count
            FROM daily_shortcut_stats
            ORDER BY stat_date, shortcut_code;
            """,
            reader => [reader.GetString(0), reader.GetString(1), I64(reader, 2)]));
        written.Add(Write(
            directory,
            "keypulse-daily-key-stats-" + stamp + ".csv",
            "stat_date,key_code,press_count",
            connection,
            """
            SELECT stat_date, key_code, press_count
            FROM daily_key_stats
            ORDER BY stat_date, key_code;
            """,
            reader =>
            [
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2).ToString(CultureInfo.InvariantCulture)
            ]));
        written.Add(Write(
            directory,
            "keypulse-daily-mouse-stats-" + stamp + ".csv",
            "stat_date,left_click_count,right_click_count,middle_click_count,xbutton1_click_count,xbutton2_click_count,wheel_up_count,wheel_down_count,wheel_left_count,wheel_right_count,legacy_raw_distance,cursor_distance_pixels,estimated_distance_meters",
            connection,
            """
            SELECT stat_date,
                   left_click_count, right_click_count, middle_click_count,
                   xbutton1_click_count, xbutton2_click_count,
                   wheel_up_count, wheel_down_count, wheel_left_count, wheel_right_count,
                   mouse_distance_pixels, cursor_distance_pixels, estimated_distance_meters
            FROM daily_mouse_stats
            ORDER BY stat_date;
            """,
            reader =>
            [
                reader.GetString(0),
                I64(reader, 1), I64(reader, 2), I64(reader, 3), I64(reader, 4), I64(reader, 5),
                I64(reader, 6), I64(reader, 7), I64(reader, 8), I64(reader, 9),
                F64(reader, 10), F64(reader, 11), F64(reader, 12)
            ]));
        written.Add(Write(
            directory,
            "keypulse-hourly-activity-" + stamp + ".csv",
            "stat_date,stat_hour,key_press_count,mouse_click_count,wheel_event_count,legacy_raw_distance,effective_active_seconds,cursor_distance_pixels,estimated_distance_meters",
            connection,
            """
            SELECT stat_date, stat_hour, key_press_count, mouse_click_count,
                   wheel_event_count, mouse_distance_pixels, active_seconds,
                   cursor_distance_pixels, estimated_distance_meters
            FROM hourly_activity_stats
            ORDER BY stat_date, stat_hour;
            """,
            reader =>
            [
                reader.GetString(0),
                reader.GetInt32(1).ToString(CultureInfo.InvariantCulture),
                I64(reader, 2), I64(reader, 3), I64(reader, 4),
                F64(reader, 5), I64(reader, 6), F64(reader, 7), F64(reader, 8)
            ]));
        written.Add(Write(
            directory,
            "keypulse-daily-app-stats-" + stamp + ".csv",
            "stat_date,process_name,display_name,key_press_count,mouse_click_count,wheel_event_count,legacy_raw_distance,foreground_active_seconds,cursor_distance_pixels,estimated_distance_meters,effective_active_seconds",
            connection,
            """
            SELECT s.stat_date, a.process_name, a.display_name,
                   s.key_press_count, s.mouse_click_count, s.wheel_event_count,
                   s.mouse_distance_pixels, s.active_seconds,
                   s.cursor_distance_pixels, s.estimated_distance_meters,
                   s.effective_active_seconds
            FROM daily_app_stats s
            JOIN app_registry a ON a.app_id = s.app_id
            ORDER BY s.stat_date, a.process_name;
            """,
            reader =>
            [
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? "" : reader.GetString(2),
                I64(reader, 3), I64(reader, 4), I64(reader, 5),
                F64(reader, 6), I64(reader, 7), F64(reader, 8), F64(reader, 9), I64(reader, 10)
            ]));
        written.Add(Write(
            directory,
            "keypulse-activity-sessions-" + stamp + ".csv",
            "session_id,stat_date,started_at,ended_at,effective_seconds,key_press_count,mouse_click_count,wheel_event_count",
            connection,
            """
            SELECT session_id, stat_date, started_at, ended_at, effective_seconds,
                   key_press_count, mouse_click_count, wheel_event_count
            FROM activity_sessions
            ORDER BY started_at;
            """,
            reader =>
            [
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                I64(reader, 4), I64(reader, 5), I64(reader, 6), I64(reader, 7)
            ]));
        written.Add(Write(
            directory,
            "keypulse-hourly-click-points-" + stamp + ".csv",
            "stat_date,stat_hour,layout_signature,monitor_id,x_px,y_px,button_code,click_count",
            connection,
            """
            SELECT c.stat_date, c.stat_hour, l.layout_signature, c.monitor_id,
                   c.x_px, c.y_px, c.button_code, c.click_count
            FROM hourly_click_points c
            JOIN display_layouts l ON l.layout_id = c.layout_id
            ORDER BY c.stat_date, c.stat_hour, c.layout_id, c.monitor_id, c.x_px, c.y_px;
            """,
            reader =>
            [
                reader.GetString(0), reader.GetInt32(1).ToString(CultureInfo.InvariantCulture),
                reader.GetString(2), reader.GetString(3),
                reader.GetInt32(4).ToString(CultureInfo.InvariantCulture),
                reader.GetInt32(5).ToString(CultureInfo.InvariantCulture),
                reader.GetString(6), I64(reader, 7)
            ]));

        return Task.FromResult<IReadOnlyList<string>>(written);
    }

    private static string Write(
        string directory,
        string fileName,
        string header,
        SqliteConnection connection,
        string sql,
        Func<SqliteDataReader, string[]> row)
    {
        var path = Path.Combine(directory, fileName);
        using var writer = new StreamWriter(path, false, Utf8Bom);
        writer.WriteLine(header);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            writer.WriteLine(string.Join(",", row(reader).Select(Escape)));
        }

        return path;
    }

    private static string I64(SqliteDataReader reader, int ordinal) =>
        reader.GetInt64(ordinal).ToString(CultureInfo.InvariantCulture);

    private static string F64(SqliteDataReader reader, int ordinal) =>
        reader.GetDouble(ordinal).ToString("0.###", CultureInfo.InvariantCulture);

    internal static string Escape(string value)
    {
        if (value.IndexOfAny(['"', ',', '\r', '\n']) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
