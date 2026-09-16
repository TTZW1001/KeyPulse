namespace KeyPulse.Infrastructure.Persistence;

public sealed class PersistenceOptions
{
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMinutes(5);
}
