using KeyPulse.Core.Interfaces;

namespace KeyPulse.Core;

public static class HeatRevealMaskBuilder
{
    public static float[] Build(
        PointerHeatmapResult result,
        int width,
        int height,
        string? monitorFilter = null,
        string? buttonFilter = null,
        bool relativeIntensity = true,
        bool logarithmic = false)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        var mask = new float[width * height];
        var grids = result.Densities
            .Where(grid => monitorFilter is null || grid.MonitorId == monitorFilter)
            .ToArray();
        var maximum = relativeIntensity
            ? grids.SelectMany(grid => grid.Cells).DefaultIfEmpty(1u).Max()
            : result.Densities.SelectMany(grid => grid.Cells).DefaultIfEmpty(1u).Max();
        maximum = Math.Max(1u, maximum);

        foreach (var grid in grids)
        {
            var monitor = result.Layout.Monitors.FirstOrDefault(item => item.Id == grid.MonitorId);
            if (monitor is null || grid.Width <= 0 || grid.Height <= 0) continue;
            var left = ScaleX(monitor.Left, result, width);
            var top = ScaleY(monitor.Top, result, height);
            var right = ScaleX(monitor.Left + monitor.Width, result, width);
            var bottom = ScaleY(monitor.Top + monitor.Height, result, height);
            for (var y = Math.Max(0, top); y < Math.Min(height, bottom); y++)
            {
                var gy = Math.Clamp((y - top) * grid.Height / Math.Max(1, bottom - top), 0, grid.Height - 1);
                for (var x = Math.Max(0, left); x < Math.Min(width, right); x++)
                {
                    var gx = Math.Clamp((x - left) * grid.Width / Math.Max(1, right - left), 0, grid.Width - 1);
                    var value = grid.Cells[(gy * grid.Width) + gx];
                    mask[(y * width) + x] = (float)Scale(value, maximum, logarithmic);
                }
            }
        }

        var blurRadius = Math.Clamp(Math.Min(width, height) / 55, 3, 16);
        Blur(mask, width, height, blurRadius);

        var clicks = result.Clicks.Where(point =>
            (monitorFilter is null || point.MonitorId == monitorFilter) &&
            (buttonFilter is null || point.ButtonCode == buttonFilter)).ToArray();
        var clickMaximum = relativeIntensity
            ? clicks.Select(point => point.Count).DefaultIfEmpty(1L).Max()
            : result.Clicks.Select(point => point.Count).DefaultIfEmpty(1L).Max();
        var radius = Math.Clamp(Math.Min(width, height) / 18, 10, 48);
        foreach (var point in clicks)
        {
            var monitor = result.Layout.Monitors.FirstOrDefault(item => item.Id == point.MonitorId);
            if (monitor is null) continue;
            var centerX = ScaleX(monitor.Left + point.X, result, width);
            var centerY = ScaleY(monitor.Top + point.Y, result, height);
            var strength = 0.22 + (0.33 * Scale(point.Count, Math.Max(1L, clickMaximum), logarithmic));
            AddSoftSpot(mask, width, height, centerX, centerY, radius, strength);
        }

        for (var i = 0; i < mask.Length; i++)
        {
            var value = Math.Clamp(mask[i], 0, 1);
            mask[i] = (float)Math.Clamp(1 - Math.Exp(-3.2 * value), 0, 1);
        }
        return mask;
    }

    private static int ScaleX(int x, PointerHeatmapResult result, int width) =>
        (x - result.Layout.VirtualLeft) * width / Math.Max(1, result.Layout.VirtualWidth);

    private static int ScaleY(int y, PointerHeatmapResult result, int height) =>
        (y - result.Layout.VirtualTop) * height / Math.Max(1, result.Layout.VirtualHeight);

    private static double Scale(double value, double maximum, bool logarithmic) => logarithmic
        ? Math.Log(1 + value) / Math.Log(1 + Math.Max(1, maximum))
        : value / Math.Max(1, maximum);

    private static void AddSoftSpot(float[] values, int width, int height, int centerX, int centerY, int radius, double strength)
    {
        var sigma = Math.Max(1, radius / 2.4);
        var denominator = 2 * sigma * sigma;
        for (var y = Math.Max(0, centerY - radius); y <= Math.Min(height - 1, centerY + radius); y++)
            for (var x = Math.Max(0, centerX - radius); x <= Math.Min(width - 1, centerX + radius); x++)
            {
                var distanceSquared = ((x - centerX) * (x - centerX)) + ((y - centerY) * (y - centerY));
                if (distanceSquared > radius * radius) continue;
                var addition = strength * Math.Exp(-distanceSquared / denominator);
                var index = (y * width) + x;
                values[index] = (float)Math.Min(1, values[index] + addition);
            }
    }

    private static void Blur(float[] values, int width, int height, int radius)
    {
        if (radius <= 0) return;
        var temporary = new float[values.Length];
        BoxBlur(values, temporary, width, height, radius, horizontal: true);
        BoxBlur(temporary, values, width, height, radius, horizontal: false);
        BoxBlur(values, temporary, width, height, Math.Max(1, radius / 2), horizontal: true);
        BoxBlur(temporary, values, width, height, Math.Max(1, radius / 2), horizontal: false);
    }

    private static void BoxBlur(float[] source, float[] destination, int width, int height, int radius, bool horizontal)
    {
        var window = (radius * 2) + 1;
        if (horizontal)
        {
            for (var y = 0; y < height; y++)
            {
                double sum = 0;
                for (var offset = -radius; offset <= radius; offset++)
                    sum += source[(y * width) + Math.Clamp(offset, 0, width - 1)];
                for (var x = 0; x < width; x++)
                {
                    destination[(y * width) + x] = (float)(sum / window);
                    var removeX = Math.Clamp(x - radius, 0, width - 1);
                    var addX = Math.Clamp(x + radius + 1, 0, width - 1);
                    sum += source[(y * width) + addX] - source[(y * width) + removeX];
                }
            }
        }
        else
        {
            for (var x = 0; x < width; x++)
            {
                double sum = 0;
                for (var offset = -radius; offset <= radius; offset++)
                    sum += source[(Math.Clamp(offset, 0, height - 1) * width) + x];
                for (var y = 0; y < height; y++)
                {
                    destination[(y * width) + x] = (float)(sum / window);
                    var removeY = Math.Clamp(y - radius, 0, height - 1);
                    var addY = Math.Clamp(y + radius + 1, 0, height - 1);
                    sum += source[(addY * width) + x] - source[(removeY * width) + x];
                }
            }
        }
    }
}
