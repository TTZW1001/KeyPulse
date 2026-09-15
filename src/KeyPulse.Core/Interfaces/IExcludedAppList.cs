namespace KeyPulse.Core.Interfaces;

public interface IExcludedAppList
{
    bool IsExcluded(string processName);

    IReadOnlyList<string> Snapshot();

    IReadOnlyList<ExcludedAppEntry> Entries();

    void Exclude(string processName);

    void Remove(string processName);

    event Action? Changed;
}
