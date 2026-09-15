using KeyPulse.Core.Events;

namespace KeyPulse.Core.Interfaces;

public interface IInputCapture
{
    event EventHandler<InputEvent>? InputReceived;

    bool IsListening { get; }

    string? Error { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();
}
