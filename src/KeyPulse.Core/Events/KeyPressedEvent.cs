using KeyPulse.Core.Models;

namespace KeyPulse.Core.Events;

public sealed record KeyPressedEvent : InputEvent
{
    public required KeyCode Key { get; init; }

    public bool IsKeyDown { get; init; } = true;

    public KeyboardModifiers ObservedModifiers { get; init; }

    public bool RecoveredFromScanCode { get; init; }

    public ushort VirtualKey { get; init; }

    public ushort ScanCode { get; init; }

    public ushort RawFlags { get; init; }
}
