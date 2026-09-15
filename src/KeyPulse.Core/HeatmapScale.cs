namespace KeyPulse.Core;

public static class HeatmapScale
{
    public static double Normalize(long count, long max)
    {
        if (count <= 0 || max <= 0)
        {
            return 0;
        }

        return Math.Log(1 + count) / Math.Log(1 + max);
    }
}
