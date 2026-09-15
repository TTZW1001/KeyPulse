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
            "stat_date,left_click_count,right_click_count,middle_click_count,xbutton1_click_count,xbutton2_click_count,wheel_up_count,wheel_down_count,wheel_left_count,wheel_right_count,mouse_distance_pixels",
            connection,
            """
            SELECT stat_date,
                   left_click_count, right_click_count, middle_click_count,
                   xbutton1_click_count, xbutton2_click_count,
                   wheel_up_count, wheel_down_count, wheel_left_count, wheel_right_count,
                   mouse_distance_pixels
            FROM daily_mouse_stats
            ORDER BY stat_date;
            """,
            reader =>
            [
                reader.GetString(0),
                I64(reader, 1), I64(reader, 2), I64(reader, 3), I64(reader, 4), I64(reader, 5),
                I64(reader, 6), I64(reader, 7), I64(reader, 8), I64(reader, 9),
                reader.GetDouble(10).ToString("0.###", CultureInfo.InvariantCulture)
            ]));
        written.Add(Write(
            directory,
            "keypulse-hourly-activity-" + stamp + ".csv",
            "stat_date,stat_hour,key_press_count,mouse_click_count,wheel_event_count,mouse_distance_pixels,active_seconds",
            connection,
            """
            SELECT stat_date, stat_hour, key_press_count, mouse_click_count,
                   wheel_event_count, mouse_distance_pixels, active_seconds
            FROM hourly_activity_stats
            ORDER BY stat_date, stat_hour;
            """,
            reader =>
            [
                reader.GetString(0),
                reader.GetInt32(1).ToString(CultureInfo.InvariantCulture),
                I64(reader, 2), I64(reader, 3), I64(reader, 4),
                reader.GetDouble(5).ToString("0.###", CultureInfo.InvariantCulture),
                I64(reader, 6)
            ]));
        written.Add(Write(
            directory,
            "keypulse-daily-app-stats-" + stamp + ".csv",
            "stat_date,process_name,display_name,key_press_count,mouse_click_count,wheel_event_count,mouse_distance_pixels,active_seconds",
            connection,
            """
            SELECT s.stat_date, a.process_name, a.display_name,
                   s.key_press_count, s.mouse_click_count, s.wheel_event_count,
                   s.mouse_distance_pixels, s.active_seconds
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
                reader.GetDouble(6).ToString("0.###", CultureInfo.InvariantCulture),
                I64(reader, 7)
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

    internal static string Escape(string value)
    {
        if (value.IndexOfAny(['"', ',', '\r', '\n']) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}