using KeyPulse.Core;
using KeyPulse.Core.Events;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Models;
using KeyPulse.Infrastructure.Aggregation;
using KeyPulse.Infrastructure.Input;
using KeyPulse.Infrastructure.Persistence;
using KeyPulse.Infrastructure.System;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KeyPulse.Tests;

public sealed class V11FeatureTests
{
    [Fact]
    public void ShortcutTracker_RecordsGeneralModifierCombos_ButNotShiftOnly()
    {
        var tracker = new ShortcutTracker();
        tracker.Process(Key("LeftCtrl", true));
        tracker.Process(Key("LeftShift", true));
        Assert.Equal("Ctrl+Shift+Q", tracker.Process(Key("Q", true)));
        tracker.Process(Key("LeftCtrl", false));
        Assert.Null(tracker.Process(Key("A", true)));
    }

    [Fact]
    public void KeyboardPresets_SeparateMainDigitsFromNumpad()
    {
        var compact = KeyboardLayoutDefinition.Get(KeyboardLayoutKind.CompactLaptop);
        var tkl = KeyboardLayoutDefinition.Get(KeyboardLayoutKind.TenKeyLess);
        var full = KeyboardLayoutDefinition.Get(KeyboardLayoutKind.FullSize);

        Assert.Contains(compact.Keys, key => key.KeyCode == "1");
        Assert.DoesNotContain(compact.Keys, key => key.KeyCode.StartsWith("NumPad", StringComparison.Ordinal));
        Assert.DoesNotContain(tkl.Keys, key => key.KeyCode.StartsWith("NumPad", StringComparison.Ordinal));
        Assert.Contains(full.Keys, key => key.KeyCode == "NumPad1");
        Assert.Equal("1", KeyMapper.Map(0x31, 0, 0x02).Name);
        Assert.Equal("NumPad1", KeyMapper.Map(0x61, 0, 0x4F).Name);
        Assert.Equal("NumPadEnter", KeyMapper.Map(0x0D, 0x02, 0x1C).Name);
    }

    [Fact]
    public void CursorDistance_UsesScreenCoordinates_AndPositionDataRequiresOptIn()
    {
        var layout = Layout();
        var start = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.FromHours(8));
        var settings = new FakeSettings { ScreenPositionStatsEnabled = true };
        using var enabled = Create(settings);
        enabled.Record(Move(start, layout, 100, 100));
        enabled.Record(Move(start.AddMilliseconds(10), layout, 103, 104));
        enabled.Record(new MouseButtonEvent
        {
            Timestamp = start.AddMilliseconds(20),
            Button = MouseButton.Left,
            Position = new PointerPosition(103, 104, layout, layout.Monitors[0])
        });

        var snapshot = enabled.CaptureSnapshot();
        Assert.Equal(5, snapshot.Mouse.CursorDistancePixels, 6);
        Assert.Equal(5D / 96D * 0.0254D, snapshot.Mouse.EstimatedDistanceMeters, 9);
        var pointer = enabled.CaptureUnflushed().Pointer;
        Assert.NotNull(pointer);
        Assert.Single(pointer!.Clicks);
        Assert.NotEmpty(pointer.Densities);
        Assert.NotEmpty(pointer.OccupancyTiles);

        settings = new FakeSettings { ScreenPositionStatsEnabled = false };
        using var disabled = Create(settings);
        disabled.Record(Move(start, layout, 100, 100));
        disabled.Record(Move(start.AddMilliseconds(10), layout, 103, 104));
        disabled.Record(new MouseButtonEvent
        {
            Timestamp = start.AddMilliseconds(20),
            Button = MouseButton.Left,
            Position = new PointerPosition(103, 104, layout, layout.Monitors[0])
        });
        Assert.Equal(5, disabled.CaptureSnapshot().Mouse.CursorDistancePixels, 6);
        Assert.True(disabled.CaptureUnflushed().Pointer?.IsEmpty ?? true);
    }

    [Fact]
    public void Migration_UpgradesV1Database_WithoutLosingExistingStats()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeyPulseV1Upgrade", Guid.NewGuid().ToString("N"));
        try
        {
            var factory = new SqliteConnectionFactory(new AppPaths(root));
            using (var connection = factory.Open())
            {
                var assembly = typeof(MigrationRunner).Assembly;
                using var stream = assembly.GetManifestResourceStream(
                    "KeyPulse.Infrastructure.Persistence.Migrations.V001_Initial.sql");
                Assert.NotNull(stream);
                using var reader = new StreamReader(stream!);
                using var command = connection.CreateCommand();
                command.CommandText = reader.ReadToEnd();
                command.ExecuteNonQuery();

                command.CommandText = "INSERT INTO daily_key_stats(stat_date,key_code,press_count) VALUES ('2026-09-16','A',7);";
                command.ExecuteNonQuery();
            }

            new DatabaseInitializer(factory, new MigrationRunner()).Initialize();
            using (var upgraded = factory.Open())
            {
                Assert.Equal(2L, ScalarLong(upgraded, "SELECT version FROM schema_version WHERE id=1;"));
                Assert.Equal(7L, ScalarLong(upgraded, "SELECT press_count FROM daily_key_stats WHERE key_code='A';"));
                Assert.Equal(1L, ScalarLong(upgraded,
                    "SELECT COUNT(*) FROM pragma_table_info('daily_mouse_stats') WHERE name='cursor_distance_pixels';"));
                Assert.Equal(1L, ScalarLong(upgraded,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='hourly_click_points';"));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static InputAggregator Create(IUserSettings settings) =>
        new(new SilentCapture(), NullLogger<InputAggregator>.Instance, settings: settings);

    private static KeyPressedEvent Key(string name, bool down) => new()
    {
        Timestamp = DateTimeOffset.Now,
        Key = new KeyCode(name),
        IsKeyDown = down
    };

    private static MouseMoveEvent Move(DateTimeOffset timestamp, DisplayLayout layout, int x, int y) => new()
    {
        Timestamp = timestamp,
        DeltaX = 0,
        DeltaY = 0,
        Position = new PointerPosition(x, y, layout, layout.Monitors[0])
    };

    private static DisplayLayout Layout()
    {
        var monitor = new DisplayMonitor("display-1", 0, 0, 1920, 1080, 96, 96, true);
        return new DisplayLayout("layout-1", 0, 0, 1920, 1080, [monitor]);
    }

    private static long ScalarLong(SqliteConnection connection, string sql)
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

    private sealed class FakeSettings : IUserSettings
    {
        public bool HideToTrayHintDismissed { get; set; }
        public ThemeMode Theme { get; set; }
        public KeyboardLayoutKind KeyboardLayout { get; set; } = KeyboardLayoutKind.CompactLaptop;
        public bool KeyboardLayoutExplicitlyChosen { get; set; }
        public bool ShortcutStatsEnabled { get; set; } = true;
        public bool ScreenPositionStatsEnabled { get; set; }
        public void Save() { }
    }
}
