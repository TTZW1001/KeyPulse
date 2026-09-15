namespace KeyPulse.Core;

public static class ClickShare
{
    public static (double Left, double Right, double Middle) Percents(long left, long right, long middle)
    {
        var sum = left + right + middle;
        if (sum <= 0)
        {
            return (0, 0, 0);
        }

        return (
            100.0 * left / sum,
            100.0 * right / sum,
            100.0 * middle / sum);
    }
}
