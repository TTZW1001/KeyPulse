namespace KeyPulse.Core.Interfaces;

public interface IAppHost
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
