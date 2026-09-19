using KeyPulse.Core;
using KeyPulse.Core.Events;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Models;
using KeyPulse.Infrastructure.Aggregation;
using KeyPulse.Infrastructure.Persistence;
using KeyPulse.Infrastructure.Persistence.Repositories;
using KeyPulse.Infrastructure.System;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Compression;
using Xunit;

namespace KeyPulse.Tests;

public sealed class DataMaintenanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "KeyPulseMaintenance", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Retention_DefaultsTo90DaysForNewInstall_AndPermanentForExistingInstall()
    {
        var newPaths = new AppPaths(Path.Combine(_root, "new"));
        Assert.Equal(90, new JsonUserSettings(newPaths).PositionRetentionDays);

        var existingPaths = new AppPaths(Path.Combine(_root, "existing"));
        Directory.CreateDirectory(existingPaths.ConfigDirectory);
        File.WriteAllText(existingPaths.SettingsPath, "{}");
        Assert.Equal(0, new JsonUserSettings(existingPaths).PositionRetentionDays);
    }

    [Fact]
    public async Task BackupAndRestore_PreservesDatabaseStatistics()
    {
        var paths = new AppPaths(_root);
        var factory = new SqliteConnectionFactory(paths);
        new DatabaseInitializer(factory, new MigrationRunner()).Initialize();
        var repository = new StatisticsRepository(factory);
        var settings = new JsonUserSettings(paths);
        using (var aggregator = CreateAggregator())
        {
            aggregator.Record(new KeyPressedEvent { Timestamp = DateTimeOffset.Now, Key = new KeyCode("A") });
            await repository.FlushAsync(aggregator.SwapForFlush());
        }

        var service = new DataMaintenanceService(paths, factory, repository, settings);
        var backupDirectory = Path.Combine(_root, "exports");
        var archive = await service.CreateBackupAsync(backupDirectory);
        await repository.ClearStatisticsAsync();
        await service.RestoreBackupAsync(archive);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var restored = new StatisticsRepository(new SqliteConnectionFactory(paths));
        Assert.Equal(1, (await restored.GetKeyStatsAsync(today, today)).Single().PressCount);
    }

    [Fact]
    public async Task BackupAndRestore_PreservesManagedScreenImageAndCropMetadata()
    {
        var paths = new AppPaths(_root);
        var factory = new SqliteConnectionFactory(paths);
        new DatabaseInitializer(factory, new MigrationRunner()).Initialize();
        var repository = new StatisticsRepository(factory);
        Directory.CreateDirectory(paths.SkinsDirectory);
        var imagePath = Path.Combine(paths.SkinsDirectory, "screen-image-source.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3, 4]);
        var settings = new JsonUserSettings(paths)
        {
            ScreenImagePath = imagePath,
            ScreenImageCrop = new ScreenImageCropSettings(0, 0, 1, 1, 1.5, "layout")
        };
        settings.Save();
        var service = new DataMaintenanceService(paths, factory, repository, settings);

        var archive = await service.CreateBackupAsync(Path.Combine(_root, "exports"));
        File.Delete(imagePath);
        await service.RestoreBackupAsync(archive);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(imagePath));
        var restored = new JsonUserSettings(paths);
        Assert.Equal("layout", restored.ScreenImageCrop?.LayoutSignature);
    }

    [Fact]
    public async Task RangeClear_RemovesOnlySelectedDates()
    {
        var paths = new AppPaths(_root);
        var factory = new SqliteConnectionFactory(paths);
        new DatabaseInitializer(factory, new MigrationRunner()).Initialize();
        var repository = new StatisticsRepository(factory);
        using var aggregator = CreateAggregator();
        aggregator.Record(new KeyPressedEvent
        {
            Timestamp = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.FromHours(8)),
            Key = new KeyCode("A")
        });
        aggregator.Record(new KeyPressedEvent
        {
            Timestamp = new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.FromHours(8)),
            Key = new KeyCode("B")
        });
        await repository.FlushAsync(aggregator.SwapForFlush());

        await repository.ClearStatisticsRangeAsync(new DateOnly(2026, 9, 17), new DateOnly(2026, 9, 17));

        var rows = await repository.GetKeyStatsAsync(new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 17));
        Assert.Single(rows);
        Assert.Equal("A", rows[0].KeyCode);
    }

    [Fact]
    public async Task PositionPrune_DeletesOnlyDatedPositionRows()
    {
        var paths = new AppPaths(_root);
        var factory = new SqliteConnectionFactory(paths);
        new DatabaseInitializer(factory, new MigrationRunner()).Initialize();
        var repository = new StatisticsRepository(factory);
        using (var connection = factory.Open())
        {
            Execute(connection, "INSERT INTO display_layouts(layout_signature,virtual_left,virtual_top,virtual_width,virtual_height,created_at) VALUES ('layout',0,0,100,100,CURRENT_TIMESTAMP);");
            Execute(connection, "INSERT INTO display_monitors(layout_id,monitor_id,left_px,top_px,width_px,height_px,dpi_x,dpi_y,is_primary) VALUES (1,'m',0,0,100,100,96,96,1);");
            Execute(connection, "INSERT INTO hourly_click_points(stat_date,stat_hour,layout_id,monitor_id,x_px,y_px,button_code,click_count) VALUES ('2026-01-01',1,1,'m',1,1,'Left',1),('2026-09-17',1,1,'m',2,2,'Left',1);");
            Execute(connection, "INSERT INTO daily_key_stats(stat_date,key_code,press_count) VALUES ('2026-01-01','A',5);");
        }

        await repository.PrunePositionDataAsync(new DateOnly(2026, 9, 1));

        using var checkedConnection = factory.Open();
        Assert.Equal(1L, ScalarLong(checkedConnection, "SELECT COUNT(*) FROM hourly_click_points;"));
        Assert.Equal(1L, ScalarLong(checkedConnection, "SELECT COUNT(*) FROM daily_key_stats;"));
    }

    [Fact]
    public async Task Restore_RejectsChangedPayload_AndLeavesCurrentDatabase()
    {
        var paths = new AppPaths(_root);
        var factory = new SqliteConnectionFactory(paths);
        new DatabaseInitializer(factory, new MigrationRunner()).Initialize();
        var repository = new StatisticsRepository(factory);
        var service = new DataMaintenanceService(paths, factory, repository, new JsonUserSettings(paths));
        var archive = await service.CreateBackupAsync(Path.Combine(_root, "exports"));
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
        {
            zip.GetEntry("data/keypulse.db")!.Delete();
            var changed = zip.CreateEntry("data/keypulse.db");
            await using var stream = changed.Open();
            await stream.WriteAsync("not a database"u8.ToArray());
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreBackupAsync(archive));
        Assert.True(repository.TryPing());
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private static InputAggregator CreateAggregator() =>
        new(new SilentCapture(), NullLogger<InputAggregator>.Instance);

    private static void Execute(Microsoft.Data.Sqlite.SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long ScalarLong(Microsoft.Data.Sqlite.SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private sealed class SilentCapture : IInputCapture
    {
        public event EventHandler<InputEvent>? InputReceived { add { } remove { } }
        public bool IsListening => true;
        public string? Error => null;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
    }
}
