using Microsoft.Data.Sqlite;

namespace KeyPulse.Infrastructure.Persistence;

public sealed class DatabaseInitializer
{
    private readonly SqliteConnectionFactory _factory;
    private readonly MigrationRunner _migrations;

    public DatabaseInitializer(SqliteConnectionFactory factory, MigrationRunner migrations)
    {
        _factory = factory;
        _migrations = migrations;
    }

    public void Initialize()
    {
        using var connection = _factory.Open();
        _migrations.Apply(connection);
        EnsureCreatedAt(connection);
    }

    private static void EnsureCreatedAt(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO settings_meta (meta_key, meta_value)
            VALUES ('created_at', $value)
            ON CONFLICT(meta_key) DO NOTHING;
            """;
        command.Parameters.AddWithValue("$value", DateTimeOffset.Now.ToString("o"));
        command.ExecuteNonQuery();
    }
}
