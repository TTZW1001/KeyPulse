using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using KeyPulse.Infrastructure.Aggregation;
using KeyPulse.Infrastructure.Hosting;
using KeyPulse.Infrastructure.Input;
using KeyPulse.Infrastructure.Persistence;
using KeyPulse.Infrastructure.Persistence.Repositories;
using KeyPulse.Infrastructure.Query;
using KeyPulse.Infrastructure.System;
using Microsoft.Extensions.DependencyInjection;

namespace KeyPulse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddKeyPulseInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<IAppHost, AppHost>();
        services.AddSingleton<ILifecycleRecovery, LifecycleRecovery>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IRunKeyStore, WindowsRunKeyStore>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<IUserSettings, JsonUserSettings>();
        services.AddSingleton<IExcludedAppList, ExcludedAppList>();
        services.AddSingleton<RawKeyboardParser>();
        services.AddSingleton<RawMouseParser>();
        services.AddSingleton<DisplayLayoutProvider>();
        services.AddSingleton<IInputCapture, RawInputService>();
        services.AddSingleton<IForegroundProcessNative, WindowsForegroundProcessNative>();
        services.AddSingleton<IForegroundAppResolver, ForegroundAppResolver>();
        services.AddSingleton<ForegroundAppSampler>();
        services.AddSingleton<IForegroundAppCache>(sp => sp.GetRequiredService<ForegroundAppSampler>());
        services.AddHostedService(sp => sp.GetRequiredService<ForegroundAppSampler>());
        services.AddSingleton<InputAggregator>();
        services.AddSingleton<IStatisticsAggregator>(sp => sp.GetRequiredService<InputAggregator>());
        services.AddSingleton<IStatisticsReader>(sp => sp.GetRequiredService<InputAggregator>());
        services.AddSingleton<PersistenceOptions>();
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<MigrationRunner>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<IStatisticsRepository, StatisticsRepository>();
        services.AddSingleton<FlushService>();
        services.AddSingleton<IFlushService>(sp => sp.GetRequiredService<FlushService>());
        services.AddSingleton<IDashboardQuery, DashboardQueryService>();
        services.AddSingleton<IKeyboardQuery, KeyboardQueryService>();
        services.AddSingleton<IMouseQuery, MouseQueryService>();
        services.AddSingleton<IPointerHeatmapQuery, PointerHeatmapQueryService>();
        services.AddSingleton<ITrendQuery, TrendQueryService>();
        services.AddSingleton<IAppQuery, AppQueryService>();
        services.AddSingleton<IStatisticsExport, StatisticsExportService>();
        return services;
    }
}
