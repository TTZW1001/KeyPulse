using KeyPulse.Core.Models;

namespace KeyPulse.Core.Events;

public sealed record MouseButtonEvent : InputEvent
{
    public required MouseButton Button { get; init; }

    public MouseButtonAction Action { get; init; } = MouseButtonAction.Click;

    public PointerPosition? Position { get; init; }
}

public enum MouseButtonAction
{
    Click,
    Down,
    Up
}
