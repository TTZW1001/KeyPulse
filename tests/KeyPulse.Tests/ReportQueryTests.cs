using KeyPulse.Core;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Persistence;
using KeyPulse.Infrastructure.Persistence.Repositories;
using KeyPulse.Infrastructure.Query;
using KeyPulse.Infrastructure.System;
using Xunit;

namespace KeyPulse.Tests;

public sealed class ReportQueryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "KeyPulseReport", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CurrentWeek_UsesPreviousWeekAtSameProgress()
    {
        var factory = new SqliteConnectionFactory(new AppPaths(_root));
        new DatabaseInitializer(factory, new MigrationRunner()).Initialize();
        var repository = new StatisticsRepository(factory);
        var currentMonday = new DateOnly(2026, 9, 14);
        var previousMonday = currentMonday.AddDays(-7);
        var keys = new Dictionary<DateOnly, IReadOnlyDictionary<string, long>>();
        var hours = new Dictionary<HourBucket, HourlyActivity>();
        var sessions = new List<ActivitySession>();
        for (var i = 0; i < 5; i++)
        {
            keys[currentMonday.AddDays(i)] = new Dictionary<string, long> { ["A"] = 20 };
            keys[previousMonday.AddDays(i)] = new Dictionary<string, long> { ["A"] = 10 };
            hours[new HourBucket(currentMonday.AddDays(i), 9)] = new HourlyActivity(20, 0, 0, 0, 0, 0, 600);
            sessions.Add(new ActivitySession($"s{i}", currentMonday.AddDays(i),
                new DateTimeOffset(2026, 9, 14 + i, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 14 + i, 9, 10, 0, TimeSpan.Zero), 600, 20, 0, 0));
        }
        await repository.FlushAsync(new StatisticsBatch(
            keys,
            new Dictionary<DateOnly, IReadOnlyDictionary<string, long>>(),
            new Dictionary<DateOnly, MouseTotals>(),
            hours,
            new Dictionary<string, long>(),
            new Dictionary<DateOnly, IReadOnlyDictionary<string, AppDayTotals>>(),
            null,
            ActivitySessions: sessions));

        var report = await new ReportQueryService(repository)
            .GetAsync(ReportPeriodKind.CurrentWeek, new DateOnly(2026, 9, 18));

        Assert.Equal(currentMonday, report.From);
        Assert.Equal(previousMonday, report.ComparisonFrom);
        Assert.Equal(new DateOnly(2026, 9, 11), report.ComparisonTo);
        Assert.Equal(100, report.Current.KeyPressCount);
        Assert.Equal(50, report.Comparison.KeyPressCount);
        Assert.Equal(3000, report.Current.EffectiveActiveSeconds);
        Assert.Equal(5, report.Current.SessionCount);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
