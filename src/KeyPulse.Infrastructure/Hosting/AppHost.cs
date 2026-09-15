using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace KeyPulse.Infrastructure.Hosting;

public sealed class AppHost : IAppHost
{
    private readonly ILogger<AppHost> _logger;

    public AppHost(ILogger<AppHost> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("{Product} {Version} started", ProductInfo.Name, ProductInfo.Version);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("{Product} stopped", ProductInfo.Name);
        return Task.CompletedTask;
    }
}
