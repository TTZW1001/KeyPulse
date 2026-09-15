namespace KeyPulse.Core.Statistics;

public sealed record ForegroundApp(string ProcessName, string? DisplayName);

public sealed record ForegroundTick(
    ForegroundApp? App,
    TimeSpan Elapsed,
    DateTimeOffset Timestamp);
