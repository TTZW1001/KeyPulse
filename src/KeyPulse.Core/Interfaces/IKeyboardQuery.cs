namespace KeyPulse.Core.Interfaces;

public interface IKeyboardQuery
{
    Task<IReadOnlyDictionary<string, long>> GetKeyCountsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, long>> GetShortcutCountsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
