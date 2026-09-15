namespace KeyPulse.Core.Interfaces;

public interface ISingleInstanceService : IDisposable
{
    bool IsPrimary { get; }

    bool TryAcquire();

    void SignalShowWindow();

    void StartShowListener(Action onShowRequested);
}
