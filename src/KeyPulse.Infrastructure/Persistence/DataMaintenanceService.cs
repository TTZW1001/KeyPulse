using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using Microsoft.Data.Sqlite;

namespace KeyPulse.Infrastructure.Persistence;

public sealed record DataStatusSnapshot(
    string DatabasePath,
    long DatabaseBytes,
    DateOnly? EarliestDate,
    DateOnly? LatestDate,
    DateTimeOffset? LastSuccessfulFlush,
    int PositionRetentionDays,
    bool HasWriteError,
    bool DatabaseIntegrityOk = true);

public interface IDataMaintenanceService
{
    Task<DataStatusSnapshot> GetStatusAsync(DateTimeOffset? lastFlush, bool hasWriteError,
        CancellationToken cancellationToken = default);
    Task<string> CreateBackupAsync(string destinationDirectory, CancellationToken cancellationToken = default);
    Task RestoreBackupAsync(string archivePath, CancellationToken cancellationToken = default);
    Task VacuumAsync(CancellationToken cancellationToken = default);
}

public sealed class DataMaintenanceService : IDataMaintenanceService
{
    private const string DatabaseEntry = "data/keypulse.db";
    private readonly IAppPaths _paths;
    private readonly SqliteConnectionFactory _factory;
    private readonly IStatisticsRepository _repository;
    private readonly IUserSettings _settings;

    public DataMaintenanceService(
        IAppPaths paths,
        SqliteConnectionFactory factory,
        IStatisticsRepository repository,
        IUserSettings settings)
    {
        _paths = paths;
        _factory = factory;
        _repository = repository;
        _settings = settings;
    }

    public async Task<DataStatusSnapshot> GetStatusAsync(
        DateTimeOffset? lastFlush,
        bool hasWriteError,
        CancellationToken cancellationToken = default)
    {
        var earliest = await _repository.GetEarliestStatDateAsync(cancellationToken);
        var latest = await GetLatestDateAsync(cancellationToken);
        var size = File.Exists(_paths.DatabasePath) ? new FileInfo(_paths.DatabasePath).Length : 0;
        var healthy = QuickCheck();
        return new DataStatusSnapshot(_paths.DatabasePath, size, earliest, latest, lastFlush,
            _settings.PositionRetentionDays, hasWriteError, healthy);
    }

    public async Task<string> CreateBackupAsync(
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destinationDirectory);
        var archive = Path.Combine(destinationDirectory,
            $"KeyPulse-backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        var temp = Path.Combine(Path.GetTempPath(), "KeyPulseBackup", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(temp, "data"));
            Directory.CreateDirectory(Path.Combine(temp, "config"));
            var dbCopy = Path.Combine(temp, DatabaseEntry.Replace('/', Path.DirectorySeparatorChar));
            using (var source = _factory.Open())
            using (var destination = new SqliteConnection($"Data Source={dbCopy};Pooling=False"))
            {
                await destination.OpenAsync(cancellationToken);
                source.BackupDatabase(destination);
            }

            CopyIfExists(_paths.SettingsPath, Path.Combine(temp, "config", "settings.json"));
            CopyIfExists(_paths.ExcludedAppsPath, Path.Combine(temp, "config", "excluded-apps.json"));
            CopyDirectory(_paths.SkinsDirectory, Path.Combine(temp, "skins"));
            var files = Directory.GetFiles(temp, "*", SearchOption.AllDirectories)
                .Select(path => new BackupFile(
                    Path.GetRelativePath(temp, path).Replace('\\', '/'),
                    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))))
                .ToArray();
            var manifest = new BackupManifest(ProductInfo.Name, ProductInfo.Version,
                MigrationRunner.CurrentSchemaVersion, DateTimeOffset.Now, files);
            await File.WriteAllTextAsync(Path.Combine(temp, "manifest.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
            ZipFile.CreateFromDirectory(temp, archive, CompressionLevel.Optimal, false);
            return archive;
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    public async Task RestoreBackupAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        var temp = Path.Combine(Path.GetTempPath(), "KeyPulseRestore", Guid.NewGuid().ToString("N"));
        try
        {
            ZipFile.ExtractToDirectory(archivePath, temp);
            var manifestPath = Path.Combine(temp, "manifest.json");
            var manifest = JsonSerializer.Deserialize<BackupManifest>(
                await File.ReadAllTextAsync(manifestPath, cancellationToken))
                ?? throw new InvalidDataException("备份清单无效。");
            if (manifest.SchemaVersion > MigrationRunner.CurrentSchemaVersion)
                throw new InvalidDataException("备份来自更高版本，当前版本无法恢复。");

            foreach (var file in manifest.Files)
            {
                var path = SafeEntryPath(temp, file.Path);
                if (!File.Exists(path))
                    throw new InvalidDataException("备份文件校验失败：" + file.Path);
                var expectedHash = Convert.FromHexString(file.Sha256);
                var restoredBytes = await File.ReadAllBytesAsync(path, cancellationToken);
                var actualHash = SHA256.HashData(restoredBytes);
                if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
                    throw new InvalidDataException("备份文件校验失败：" + file.Path);
            }

            var database = SafeEntryPath(temp, DatabaseEntry);
            ValidateDatabase(database);
            Directory.CreateDirectory(_paths.DataDirectory);
            Directory.CreateDirectory(_paths.ConfigDirectory);
            SqliteConnection.ClearAllPools();
            ReplaceFileAtomically(database, _paths.DatabasePath);
            CopyIfExists(Path.Combine(temp, "config", "settings.json"), _paths.SettingsPath);
            CopyIfExists(Path.Combine(temp, "config", "excluded-apps.json"), _paths.ExcludedAppsPath);
            CopyDirectory(Path.Combine(temp, "skins"), _paths.SkinsDirectory);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    public Task VacuumAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = _factory.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "VACUUM;";
        command.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    private bool QuickCheck()
    {
        if (!File.Exists(_paths.DatabasePath)) return true;
        using var connection = _factory.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        return string.Equals(Convert.ToString(command.ExecuteScalar()), "ok", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<DateOnly?> GetLatestDateAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = _factory.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT MAX(stat_date) FROM (
                SELECT stat_date FROM daily_key_stats
                UNION ALL SELECT stat_date FROM daily_mouse_stats
                UNION ALL SELECT stat_date FROM hourly_activity_stats
            );
            """;
        var value = command.ExecuteScalar();
        return value is null or DBNull
            ? null
            : DateOnly.ParseExact(Convert.ToString(value)!, "yyyy-MM-dd", global::System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void ValidateDatabase(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        if (!string.Equals(Convert.ToString(command.ExecuteScalar()), "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("备份数据库完整性检查失败。");
    }

    private static string SafeEntryPath(string root, string entry)
    {
        var rootPath = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, entry.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("备份包含非法路径。");
        return path;
    }

    private static void CopyIfExists(string source, string destination)
    {
        if (!File.Exists(source)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, true);
    }

    private static void CopyDirectory(string source, string destination)
    {
        if (!Directory.Exists(source)) return;
        foreach (var file in Directory.GetFiles(source, "*.png", SearchOption.TopDirectoryOnly))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static void ReplaceFileAtomically(string source, string destination)
    {
        var staged = destination + ".restore-staged";
        var rollback = destination + ".restore-rollback";
        File.Copy(source, staged, true);
        try
        {
            if (File.Exists(destination))
            {
                File.Replace(staged, destination, rollback, true);
                File.Delete(rollback);
            }
            else
            {
                File.Move(staged, destination);
            }
        }
        finally
        {
            if (File.Exists(staged)) File.Delete(staged);
        }
    }

    private sealed record BackupManifest(
        string Product,
        string Version,
        int SchemaVersion,
        DateTimeOffset CreatedAt,
        IReadOnlyList<BackupFile> Files);
    private sealed record BackupFile(string Path, string Sha256);
}
