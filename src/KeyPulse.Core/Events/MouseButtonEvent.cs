using KeyPulse.Core.Models;

namespace KeyPulse.Core.Events;

public sealed record MouseButtonEvent : InputEvent
{
    public required MouseButton Button { get; init; }

    public PointerPosition? Position { get; init; }
}
