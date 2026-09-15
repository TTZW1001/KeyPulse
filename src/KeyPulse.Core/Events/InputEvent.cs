namespace KeyPulse.Core.Events;

public abstract record InputEvent
{
    public DateTimeOffset Timestamp { get; init; }
}
