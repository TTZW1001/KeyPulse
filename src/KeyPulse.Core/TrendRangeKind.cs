namespace KeyPulse.Core;

public enum TrendRangeKind
{
    Today,
    Yesterday,
    Last7Days,
    Last30Days,
    ThisMonth,
    All,
    Custom
}

public static class TrendRangeResolver
{
    public const int MaxCustomSpanDays = 366;

    public static (DateOnly From, DateOnly To) Resolve(
        TrendRangeKind kind,
        DateOnly today,
        DateOnly? earliest,
        DateOnly? customFrom,
        DateOnly? customTo)
    {
        var (from, to) = kind switch
        {
            TrendRangeKind.Today => (today, today),
            TrendRangeKind.Yesterday => (today.AddDays(-1), today.AddDays(-1)),
            TrendRangeKind.Last7Days => (today.AddDays(-6), today),
            TrendRangeKind.Last30Days => (today.AddDays(-29), today),
            TrendRangeKind.ThisMonth => (new DateOnly(today.Year, today.Month, 1), today),
            TrendRangeKind.All => (earliest ?? today, today),
            TrendRangeKind.Custom => (
                customFrom ?? today.AddDays(-6),
                customTo ?? today),
            _ => (today, today)
        };

        if (from > to)
        {
            (from, to) = (to, from);
        }

        if (kind == TrendRangeKind.Custom)
        {
            var span = to.DayNumber - from.DayNumber + 1;
            if (span > MaxCustomSpanDays)
            {
                from = to.AddDays(-(MaxCustomSpanDays - 1));
            }
        }

        return (from, to);
    }
}
