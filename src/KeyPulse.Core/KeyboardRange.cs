namespace KeyPulse.Core;

public enum KeyboardRange
{
    Today,
    Last7Days,
    Last30Days,
    All
}

public static class KeyboardRangeExtensions
{
    public static DateOnly GetStartDate(this KeyboardRange range, DateOnly today) => range switch
    {
        KeyboardRange.Today => today,
        KeyboardRange.Last7Days => today.AddDays(-6),
        KeyboardRange.Last30Days => today.AddDays(-29),
        KeyboardRange.All => DateOnly.MinValue,
        _ => today.AddDays(-6)
    };
}
