namespace KeyPulse.Core.Interfaces;

public interface ILifecycleRecovery
{
    Task OnSuspendAsync(CancellationToken cancellationToken = default);

    Task OnResumeAsync(CancellationToken cancellationToken = default);

    Task OnSessionEndingAsync(CancellationToken cancellationToken = default);
}
