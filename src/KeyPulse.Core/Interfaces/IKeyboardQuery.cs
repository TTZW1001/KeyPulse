namespace KeyPulse.Core.Interfaces;

public interface IKeyboardQuery
{
    Task<IReadOnlyDictionary<string, long>> GetKeyCountsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
