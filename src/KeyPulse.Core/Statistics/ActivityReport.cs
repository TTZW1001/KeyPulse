namespace KeyPulse.Core.Statistics;

public sealed record ReportTotals(
    long KeyPressCount,
    long MouseClickCount,
    long WheelEventCount,
    long EffectiveActiveSeconds,
    int SessionCount,
    double DistancePixels)
{
    public long ActivityCount => KeyPressCount + MouseClickCount + WheelEventCount;
}

public sealed record ReportSeriesPoint(
    DateOnly Date,
    string Label,
    long ActivityCount,
    long EffectiveActiveSeconds);

public sealed record ActivityReport(
    ReportPeriodKind Period,
    DateOnly From,
    DateOnly To,
    DateOnly ComparisonFrom,
    DateOnly ComparisonTo,
    ReportTotals Current,
    ReportTotals Comparison,
    string? TopKey,
    long TopKeyCount,
    string? TopApp,
    long TopAppSeconds,
    IReadOnlyList<ReportSeriesPoint> Series,
    bool HasEffectiveTime);
