namespace KeyPulse.Core.Events;

public sealed record MouseWheelEvent : InputEvent
{
    public required int Delta { get; init; }

    public bool Horizontal { get; init; }
}
