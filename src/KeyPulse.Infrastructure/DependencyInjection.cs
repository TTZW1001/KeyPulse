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
        services.AddSingleton<IRunKeyStore, WindowsRunKeyStore>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<IUserSettings, JsonUserSettings>();
        services.AddSingleton<RawKeyboardParser>();
        services.AddSingleton<RawMouseParser>();
        services.AddSingleton<IInputCapture, RawInputService>();
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
        return services;
    }
}
