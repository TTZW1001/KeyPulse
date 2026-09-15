using KeyPulse.Core.Interfaces;
using KeyPulse.Infrastructure.Hosting;
using KeyPulse.Infrastructure.System;
using Microsoft.Extensions.DependencyInjection;

namespace KeyPulse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddKeyPulseInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<IAppHost, AppHost>();
        return services;
    }
}
