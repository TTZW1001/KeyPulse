using System.Reflection;
using Microsoft.Data.Sqlite;

namespace KeyPulse.Infrastructure.Persistence;

public sealed class MigrationRunner
{
    public const int CurrentSchemaVersion = 2;

    public void Apply(SqliteConnection connection)
    {
        var version = ReadVersion(connection);
        if (version >= CurrentSchemaVersion)
        {
            return;
        }

        if (version < 1)
        {
            using var transaction = connection.BeginTransaction();
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = LoadSql("V001_Initial.sql");
                command.ExecuteNonQuery();
            }

            transaction.Commit();
            version = 1;
        }

        if (version < 2)
        {
            using var transaction = connection.BeginTransaction();
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = LoadSql("V002_InteractionHeatmaps.sql");
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    private static int ReadVersion(SqliteConnection connection)
    {
        using (var exists = connection.CreateCommand())
        {
            exists.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'schema_version';";
            if (Convert.ToInt32(exists.ExecuteScalar()) == 0)
            {
                return 0;
            }
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE((SELECT version FROM schema_version WHERE id = 1), 0);";
        var value = command.ExecuteScalar();
        return value is long l ? (int)l : Convert.ToInt32(value);
    }

    private static string LoadSql(string fileName)
    {
        var assembly = typeof(MigrationRunner).Assembly;
        var resourceName = "KeyPulse.Infrastructure.Persistence.Migrations." + fileName;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Missing migration resource: " + resourceName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
