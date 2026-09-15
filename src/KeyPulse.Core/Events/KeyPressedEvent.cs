using KeyPulse.Core.Models;

namespace KeyPulse.Core.Events;

public sealed record KeyPressedEvent : InputEvent
{
    public required KeyCode Key { get; init; }
}
