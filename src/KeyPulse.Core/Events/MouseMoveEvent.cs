namespace KeyPulse.Core.Events;

using KeyPulse.Core.Models;

public sealed record MouseMoveEvent : InputEvent
{
    public required int DeltaX { get; init; }

    public required int DeltaY { get; init; }

    public PointerPosition? Position { get; init; }

    public PointerPosition? PreviousTrajectoryPosition { get; init; }

    public double CursorDistancePixels { get; init; }

    public double EstimatedDistanceMeters { get; init; }
}
