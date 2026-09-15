namespace KeyPulse.Core.Events;

public sealed record MouseMoveEvent : InputEvent
{
    public required int DeltaX { get; init; }

    public required int DeltaY { get; init; }
}
