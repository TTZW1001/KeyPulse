namespace KeyPulse.Core.Interfaces;

public interface IExcludedAppList
{
    bool IsExcluded(string processName);

    IReadOnlyList<string> Snapshot();

    void Exclude(string processName);

    event Action? Changed;
}
