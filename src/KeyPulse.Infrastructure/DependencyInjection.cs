using KeyPulse.Core.Interfaces;
using KeyPulse.Infrastructure.Aggregation;
using KeyPulse.Infrastructure.Hosting;
using KeyPulse.Infrastructure.Input;
using KeyPulse.Infrastructure.System;
using Microsoft.Extensions.DependencyInjection;

namespace KeyPulse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddKeyPulseInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<IAppHost, AppHost>();
        services.AddSingleton<RawKeyboardParser>();
        services.AddSingleton<RawMouseParser>();
        services.AddSingleton<IInputCapture, RawInputService>();
        services.AddSingleton<InputAggregator>();
        services.AddSingleton<IStatisticsAggregator>(sp => sp.GetRequiredService<InputAggregator>());
        services.AddSingleton<IStatisticsReader>(sp => sp.GetRequiredService<InputAggregator>());
        return services;
    }
}
