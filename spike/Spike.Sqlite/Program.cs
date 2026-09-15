using Microsoft.Data.Sqlite;

namespace Spike.Sqlite;

internal static class Program
{
    private static int Main()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KeyPulseSpike");
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "spike.db");

        try
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }

            foreach (var leftover in Directory.GetFiles(dir, "spike.db*"))
            {
                File.Delete(leftover);
            }

            Console.WriteLine("DB path: " + dbPath);

            using (var init = Open(dbPath))
            {
                Execute(init, "PRAGMA journal_mode = WAL;");
                Execute(init, "PRAGMA synchronous = NORMAL;");
                Execute(init, "PRAGMA foreign_keys = ON;");
                Execute(init, "PRAGMA busy_timeout = 5000;");

                var journalMode = Scalar(init, "PRAGMA journal_mode;");
                Console.WriteLine("journal_mode: " + journalMode);
                if (!string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("WAL was not enabled.");
                }

                RunMigrations(init);
            }

            using (var writer = Open(dbPath))
            using (var reader = Open(dbPath))
            {
                using (var tx = writer.BeginTransaction())
                {
                    Upsert(writer, "2026-09-15", "A", 10);
                    Upsert(writer, "2026-09-15", "Space", 3);
                    tx.Commit();
                }

                var firstA = ReadCount(reader, "2026-09-15", "A");
                Console.WriteLine("After first transaction A=" + firstA);
                if (firstA != 10)
                {
                    throw new InvalidOperationException("First UPSERT did not persist.");
                }

                using (var tx = writer.BeginTransaction())
                {
                    Upsert(writer, "2026-09-15", "A", 5);
                    tx.Commit();
                }

                var secondA = ReadCount(reader, "2026-09-15", "A");
                Console.WriteLine("After incremental UPSERT A=" + secondA);
                if (secondA != 15)
                {
                    throw new InvalidOperationException("UPSERT did not increment. Got " + secondA);
                }

                var rollbackBefore = ReadCount(reader, "2026-09-15", "Space");
                try
                {
                    using var tx = writer.BeginTransaction();
                    Upsert(writer, "2026-09-15", "Space", 100);
                    throw new InvalidOperationException("forced-rollback");
                }
                catch (InvalidOperationException ex) when (ex.Message == "forced-rollback")
                {
                    // transaction disposed without commit
                }

                var rollbackAfter = ReadCount(reader, "2026-09-15", "Space");
                Console.WriteLine("Rollback Space stayed " + rollbackAfter + " (before " + rollbackBefore + ")");
                if (rollbackAfter != rollbackBefore)
                {
                    throw new InvalidOperationException("Uncommitted transaction leaked.");
                }

                var start = DateTime.UtcNow;
                var writerTask = Task.Run(() =>
                {
                    using var conn = Open(dbPath);
                    for (var i = 0; i < 200; i++)
                    {
                        using var tx = conn.BeginTransaction();
                        Upsert(conn, "2026-09-15", "A", 1);
                        tx.Commit();
                    }
                });

                var readerTask = Task.Run(() =>
                {
                    using var conn = Open(dbPath);
                    long last = -1;
                    for (var i = 0; i < 200; i++)
                    {
                        last = ReadCount(conn, "2026-09-15", "A");
                    }

                    return last;
                });

                Task.WaitAll(writerTask, readerTask);
                var concurrentA = ReadCount(reader, "2026-09-15", "A");
                Console.WriteLine("After concurrent write/read A=" + concurrentA + " in " + (DateTime.UtcNow - start).TotalMilliseconds.ToString("0") + " ms");
                if (concurrentA != 215)
                {
                    throw new InvalidOperationException("Concurrent UPSERT total expected 215, got " + concurrentA);
                }

                var version = Scalar(reader, "SELECT version FROM schema_version WHERE id = 1;");
                Console.WriteLine("schema_version: " + version);

                var walPath = dbPath + "-wal";
                Console.WriteLine("WAL file exists: " + File.Exists(walPath));
                Console.WriteLine("Tables: " + Scalar(reader, "SELECT group_concat(name) FROM sqlite_master WHERE type='table' ORDER BY name;"));
            }

            Console.WriteLine("SPIKE E PASS");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("SPIKE E FAIL: " + ex);
            return 1;
        }
    }

    private static SqliteConnection Open(string dbPath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString());
        connection.Open();
        return connection;
    }

    private static void RunMigrations(SqliteConnection connection)
    {
        Execute(connection, """
            CREATE TABLE IF NOT EXISTS schema_version (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                version INTEGER NOT NULL,
                applied_at TEXT NOT NULL
            );
            """);

        var current = Scalar(connection, "SELECT COALESCE((SELECT version FROM schema_version WHERE id = 1), 0);");
        var version = int.Parse(current ?? "0");
        Console.WriteLine("migration current version: " + version);

        if (version < 1)
        {
            using var tx = connection.BeginTransaction();
            Execute(connection, """
                CREATE TABLE IF NOT EXISTS spike_key_stats (
                    stat_date TEXT NOT NULL,
                    key_code TEXT NOT NULL,
                    press_count INTEGER NOT NULL DEFAULT 0 CHECK (press_count >= 0),
                    PRIMARY KEY (stat_date, key_code)
                );
                """);
            Execute(connection, """
                INSERT INTO schema_version (id, version, applied_at)
                VALUES (1, 1, datetime('now'))
                ON CONFLICT(id) DO UPDATE SET
                    version = excluded.version,
                    applied_at = excluded.applied_at;
                """);
            tx.Commit();
            Console.WriteLine("applied V001_Initial");
        }
    }

    private static void Upsert(SqliteConnection connection, string date, string key, long count)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO spike_key_stats (stat_date, key_code, press_count)
            VALUES ($date, $key, $count)
            ON CONFLICT(stat_date, key_code)
            DO UPDATE SET press_count = press_count + excluded.press_count;
            """;
        cmd.Parameters.AddWithValue("$date", date);
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$count", count);
        cmd.ExecuteNonQuery();
    }

    private static long ReadCount(SqliteConnection connection, string date, string key)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(SUM(press_count), 0)
            FROM spike_key_stats
            WHERE stat_date = $date AND key_code = $key;
            """;
        cmd.Parameters.AddWithValue("$date", date);
        cmd.Parameters.AddWithValue("$key", key);
        var value = cmd.ExecuteScalar();
        return value is long l ? l : Convert.ToInt64(value);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string? Scalar(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar()?.ToString();
    }
}
