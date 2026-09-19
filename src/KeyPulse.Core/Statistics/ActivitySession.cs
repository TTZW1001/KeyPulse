namespace KeyPulse.Core.Statistics;

public sealed record ActivitySession(
    string SessionId,
    DateOnly Date,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    long EffectiveSeconds,
    long KeyPressCount = 0,
    long MouseClickCount = 0,
    long WheelEventCount = 0)
{
    public long WallClockSeconds => Math.Max(0, (long)(EndedAt - StartedAt).TotalSeconds);
}
