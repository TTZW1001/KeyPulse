namespace KeyPulse.Core.Interfaces;

public interface IStartupService
{
    bool IsEnabled { get; }

    string CommandLine { get; }

    void Enable();

    void Disable();
}
