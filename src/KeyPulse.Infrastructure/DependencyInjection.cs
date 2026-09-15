using KeyPulse.Core.Interfaces;
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
        return services;
    }
}
