using System.Windows.Media;
using System.Windows.Media.Imaging;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using Color = System.Windows.Media.Color;

namespace KeyPulse.App.Services;

public enum PointerHeatmapMode
{
    Clicks,
    Trajectory,
    ImageReveal
}

public sealed class PointerHeatmapRenderer
{
    private readonly ScreenImageService _screenImages;

    public PointerHeatmapRenderer(ScreenImageService screenImages) => _screenImages = screenImages;

    public bool HasReadyScreenImage(PointerHeatmapResult result) =>
        _screenImages.GetState(result.Layout) == ScreenImageState.Ready;

    public BitmapSource Render(
        PointerHeatmapResult result,
        PointerHeatmapMode mode,
        string? monitorFilter = null,
        string? buttonFilter = null,
        bool relativeIntensity = true,
        bool useLogScale = false,
        Color? accent = null,
        int maxDimension = 720)
    {
        maxDimension = Math.Clamp(maxDimension, 1, 2048);
        var scale = Math.Min(
            maxDimension / (double)Math.Max(1, result.Layout.VirtualWidth),
            maxDimension / (double)Math.Max(1, result.Layout.VirtualHeight));
        var width = Math.Max(1, (int)Math.Round(result.Layout.VirtualWidth * scale));
        var height = Math.Max(1, (int)Math.Round(result.Layout.VirtualHeight * scale));
        if (mode == PointerHeatmapMode.ImageReveal &&
            _screenImages.TryLoadCroppedPixels(result.Layout, width, height, out var sourcePixels))
        {
            return RenderImageReveal(result, sourcePixels, width, height, monitorFilter, buttonFilter,
                relativeIntensity, useLogScale);
        }

        return RenderPaletteHeatmap(result, mode, width, height, monitorFilter, buttonFilter,
            relativeIntensity, useLogScale, accent ?? Color.FromRgb(0x4E, 0x6E, 0x9E));
    }

    private static BitmapSource RenderImageReveal(
        PointerHeatmapResult result,
        byte[] source,
        int width,
        int height,
        string? monitorFilter,
        string? buttonFilter,
        bool relativeIntensity,
        bool useLogScale)
    {
        var blurred = Blur(source, width, height, Math.Clamp(Math.Min(width, height) / 42, 5, 18));
        var mask = HeatRevealMaskBuilder.Build(result, width, height, monitorFilter, buttonFilter,
            relativeIntensity, useLogScale);
        var output = new byte[source.Length];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var index = ((y * width) + x) * 4;
                if (!InsideMonitor(result, x, y, width, height))
                {
                    output[index] = 0x21; output[index + 1] = 0x21; output[index + 2] = 0x21; output[index + 3] = 0xFF;
                    continue;
                }

                var reveal = Math.Clamp(mask[(y * width) + x], 0, 1);
                var cleared = SmoothStep(0.06, 0.96, reveal);
                var luminance = (0.114 * blurred[index]) + (0.587 * blurred[index + 1]) +
                                (0.299 * blurred[index + 2]);
                const double retainedColor = 0.18;
                const double coldBrightness = 0.64;
                const double neutralGray = 145;
                const double neutralGrayMix = 0.38;
                var frostedB = Mix(Mix(luminance, blurred[index], retainedColor) * coldBrightness,
                    neutralGray, neutralGrayMix);
                var frostedG = Mix(Mix(luminance, blurred[index + 1], retainedColor) * coldBrightness,
                    neutralGray, neutralGrayMix);
                var frostedR = Mix(Mix(luminance, blurred[index + 2], retainedColor) * coldBrightness,
                    neutralGray, neutralGrayMix);
                output[index] = ToByte(Mix(frostedB, source[index], cleared));
                output[index + 1] = ToByte(Mix(frostedG, source[index + 1], cleared));
                output[index + 2] = ToByte(Mix(frostedR, source[index + 2], cleared));
                output[index + 3] = 0xFF;
            }
        return CreateBitmap(output, width, height);
    }

    private static BitmapSource RenderPaletteHeatmap(
        PointerHeatmapResult result,
        PointerHeatmapMode mode,
        int width,
        int height,
        string? monitorFilter,
        string? buttonFilter,
        bool relativeIntensity,
        bool useLogScale,
        Color accent)
    {
        var pixels = new byte[width * height * 4];
        foreach (var monitor in result.Layout.Monitors)
        {
            if (monitorFilter is not null && monitor.Id != monitorFilter) continue;
            var left = ScaleX(monitor.Left, result, width);
            var top = ScaleY(monitor.Top, result, height);
            var right = ScaleX(monitor.Left + monitor.Width, result, width);
            var bottom = ScaleY(monitor.Top + monitor.Height, result, height);
            FillRect(pixels, width, height, left, top, right, bottom, 0xEC, 0xEC, 0xE9, 0xFF);
        }

        if (mode == PointerHeatmapMode.Clicks)
        {
            var visible = result.Clicks.Where(point =>
                (monitorFilter is null || point.MonitorId == monitorFilter) &&
                (buttonFilter is null || point.ButtonCode == buttonFilter)).ToArray();
            var maximum = relativeIntensity
                ? visible.Select(point => point.Count).DefaultIfEmpty(1L).Max()
                : result.Clicks.Select(point => point.Count).DefaultIfEmpty(1L).Max();
            foreach (var point in visible)
            {
                var monitor = result.Layout.Monitors.FirstOrDefault(item => item.Id == point.MonitorId);
                if (monitor is null) continue;
                var x = ScaleX(monitor.Left + point.X, result, width);
                var y = ScaleY(monitor.Top + point.Y, result, height);
                DrawDot(pixels, width, height, x, y, 7, Scale(point.Count, maximum, useLogScale), accent);
            }
        }
        else
        {
            var useDensity = mode == PointerHeatmapMode.Trajectory;
            var grids = useDensity
                ? result.Densities.Select(grid => (grid.MonitorId, grid.Width, grid.Height,
                    Values: grid.Cells.Select(value => (double)value).ToArray()))
                    .Where(grid => monitorFilter is null || grid.MonitorId == monitorFilter).ToList()
                : result.Coverages.Select(grid => (grid.MonitorId, grid.Width, grid.Height,
                    Values: grid.Cells.Select(value => (double)value).ToArray()))
                    .Where(grid => monitorFilter is null || grid.MonitorId == monitorFilter).ToList();
            var globalValues = useDensity
                ? result.Densities.SelectMany(grid => grid.Cells.Select(value => (double)value))
                : result.Coverages.SelectMany(grid => grid.Cells.Select(value => (double)value));
            var maximum = relativeIntensity
                ? Math.Max(1, grids.SelectMany(grid => grid.Values).DefaultIfEmpty(1).Max())
                : Math.Max(1, globalValues.DefaultIfEmpty(1).Max());
            foreach (var grid in grids)
            {
                var monitor = result.Layout.Monitors.FirstOrDefault(item => item.Id == grid.MonitorId);
                if (monitor is null) continue;
                for (var gy = 0; gy < grid.Height; gy++)
                    for (var gx = 0; gx < grid.Width; gx++)
                    {
                        var value = grid.Values[(gy * grid.Width) + gx];
                        if (value <= 0) continue;
                        var x0 = ScaleX(monitor.Left + (gx * monitor.Width / grid.Width), result, width);
                        var y0 = ScaleY(monitor.Top + (gy * monitor.Height / grid.Height), result, height);
                        var x1 = ScaleX(monitor.Left + (((gx + 1) * monitor.Width + grid.Width - 1) / grid.Width), result, width);
                        var y1 = ScaleY(monitor.Top + (((gy + 1) * monitor.Height + grid.Height - 1) / grid.Height), result, height);
                        FillHeatCell(pixels, width, height, x0, y0, Math.Max(x0 + 1, x1), Math.Max(y0 + 1, y1),
                            useDensity ? Scale(value, maximum, useLogScale) : value / maximum, accent);
                    }
            }
        }
        return CreateBitmap(pixels, width, height);
    }

    private static bool InsideMonitor(PointerHeatmapResult result, int x, int y, int width, int height)
    {
        var virtualX = result.Layout.VirtualLeft + x * result.Layout.VirtualWidth / Math.Max(1, width);
        var virtualY = result.Layout.VirtualTop + y * result.Layout.VirtualHeight / Math.Max(1, height);
        return result.Layout.Monitors.Any(monitor => virtualX >= monitor.Left && virtualX < monitor.Left + monitor.Width &&
                                                     virtualY >= monitor.Top && virtualY < monitor.Top + monitor.Height);
    }

    private static byte[] Blur(byte[] source, int width, int height, int radius)
    {
        var first = new byte[source.Length];
        var second = new byte[source.Length];
        BoxBlur(source, first, width, height, radius, true);
        BoxBlur(first, second, width, height, radius, false);
        BoxBlur(second, first, width, height, Math.Max(2, radius / 2), true);
        BoxBlur(first, second, width, height, Math.Max(2, radius / 2), false);
        return second;
    }

    private static void BoxBlur(byte[] source, byte[] destination, int width, int height, int radius, bool horizontal)
    {
        var window = (radius * 2) + 1;
        if (horizontal)
        {
            for (var y = 0; y < height; y++)
            {
                long b = 0, g = 0, r = 0, a = 0;
                for (var offset = -radius; offset <= radius; offset++)
                    AddPixel(source, ((y * width) + Math.Clamp(offset, 0, width - 1)) * 4, ref b, ref g, ref r, ref a, 1);
                for (var x = 0; x < width; x++)
                {
                    WritePixel(destination, ((y * width) + x) * 4, b, g, r, a, window);
                    AddPixel(source, ((y * width) + Math.Clamp(x - radius, 0, width - 1)) * 4, ref b, ref g, ref r, ref a, -1);
                    AddPixel(source, ((y * width) + Math.Clamp(x + radius + 1, 0, width - 1)) * 4, ref b, ref g, ref r, ref a, 1);
                }
            }
        }
        else
        {
            for (var x = 0; x < width; x++)
            {
                long b = 0, g = 0, r = 0, a = 0;
                for (var offset = -radius; offset <= radius; offset++)
                    AddPixel(source, ((Math.Clamp(offset, 0, height - 1) * width) + x) * 4, ref b, ref g, ref r, ref a, 1);
                for (var y = 0; y < height; y++)
                {
                    WritePixel(destination, ((y * width) + x) * 4, b, g, r, a, window);
                    AddPixel(source, ((Math.Clamp(y - radius, 0, height - 1) * width) + x) * 4, ref b, ref g, ref r, ref a, -1);
                    AddPixel(source, ((Math.Clamp(y + radius + 1, 0, height - 1) * width) + x) * 4, ref b, ref g, ref r, ref a, 1);
                }
            }
        }
    }

    private static void AddPixel(byte[] pixels, int index, ref long b, ref long g, ref long r, ref long a, int sign)
    {
        b += sign * pixels[index]; g += sign * pixels[index + 1];
        r += sign * pixels[index + 2]; a += sign * pixels[index + 3];
    }

    private static void WritePixel(byte[] pixels, int index, long b, long g, long r, long a, int divisor)
    {
        pixels[index] = (byte)(b / divisor); pixels[index + 1] = (byte)(g / divisor);
        pixels[index + 2] = (byte)(r / divisor); pixels[index + 3] = (byte)(a / divisor);
    }

    private static int ScaleX(int x, PointerHeatmapResult result, int width) =>
        (x - result.Layout.VirtualLeft) * width / Math.Max(1, result.Layout.VirtualWidth);
    private static int ScaleY(int y, PointerHeatmapResult result, int height) =>
        (y - result.Layout.VirtualTop) * height / Math.Max(1, result.Layout.VirtualHeight);

    private static BitmapSource CreateBitmap(byte[] pixels, int width, int height)
    {
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        image.Freeze();
        return image;
    }

    private static byte ToByte(double value) =>
        (byte)Math.Clamp(Math.Round(value), 0, 255);

    private static double Mix(double from, double to, double amount) =>
        from + ((to - from) * Math.Clamp(amount, 0, 1));

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        var normalized = Math.Clamp((value - edge0) / Math.Max(0.0001, edge1 - edge0), 0, 1);
        return normalized * normalized * (3 - (2 * normalized));
    }

    private static void FillRect(byte[] pixels, int width, int height, int left, int top, int right, int bottom,
        byte r, byte g, byte b, byte a)
    {
        for (var y = Math.Max(0, top); y < Math.Min(height, bottom); y++)
            for (var x = Math.Max(0, left); x < Math.Min(width, right); x++)
            {
                var index = ((y * width) + x) * 4;
                pixels[index] = b; pixels[index + 1] = g; pixels[index + 2] = r; pixels[index + 3] = a;
            }
    }

    private static void DrawDot(byte[] pixels, int width, int height, int centerX, int centerY, int radius,
        double intensity, Color accent)
    {
        for (var y = centerY - radius; y <= centerY + radius; y++)
            for (var x = centerX - radius; x <= centerX + radius; x++)
            {
                var distance = Math.Sqrt(((x - centerX) * (x - centerX)) + ((y - centerY) * (y - centerY)));
                if (distance <= radius) SetHeatPixel(pixels, width, height, x, y,
                    intensity * (1 - distance / radius), accent);
            }
    }

    private static void FillHeatCell(byte[] pixels, int width, int height, int left, int top, int right, int bottom,
        double intensity, Color accent)
    {
        for (var y = Math.Max(0, top); y < Math.Min(height, bottom); y++)
            for (var x = Math.Max(0, left); x < Math.Min(width, right); x++)
                SetHeatPixel(pixels, width, height, x, y, intensity, accent);
    }

    private static void SetHeatPixel(byte[] pixels, int width, int height, int x, int y, double intensity, Color accent)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return;
        intensity = Math.Clamp(intensity, 0.08, 1);
        var index = ((y * width) + x) * 4;
        pixels[index] = (byte)(pixels[index] * (1 - intensity) + accent.B * intensity);
        pixels[index + 1] = (byte)(pixels[index + 1] * (1 - intensity) + accent.G * intensity);
        pixels[index + 2] = (byte)(pixels[index + 2] * (1 - intensity) + accent.R * intensity);
        pixels[index + 3] = 0xFF;
    }

    private static double Scale(double value, double maximum, bool logarithmic) => logarithmic
        ? Math.Log(1 + value) / Math.Log(1 + Math.Max(1, maximum))
        : value / Math.Max(1, maximum);
}
