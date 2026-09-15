using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace KeyPulse.App.Services;

internal static class TrayIconLoader
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Load(string path, bool grayscale)
    {
        using var source = new Bitmap(path);
        using var sized = new Bitmap(source, new Size(32, 32));
        if (!grayscale)
        {
            return FromBitmap(sized);
        }

        using var gray = ToGrayscale(sized);
        return FromBitmap(gray);
    }

    private static Icon FromBitmap(Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static Bitmap ToGrayscale(Bitmap source)
    {
        var dest = new Bitmap(source.Width, source.Height);
        using var graphics = Graphics.FromImage(dest);
        var matrix = new ColorMatrix(new[]
        {
            new[] { 0.299f, 0.299f, 0.299f, 0f, 0f },
            new[] { 0.587f, 0.587f, 0.587f, 0f, 0f },
            new[] { 0.114f, 0.114f, 0.114f, 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { 0f, 0f, 0f, 0f, 1f }
        });
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(matrix);
        graphics.DrawImage(
            source,
            new Rectangle(0, 0, source.Width, source.Height),
            0,
            0,
            source.Width,
            source.Height,
            GraphicsUnit.Pixel,
            attributes);
        return dest;
    }
}
