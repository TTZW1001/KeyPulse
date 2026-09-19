using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Models;

namespace KeyPulse.App.Services;

public enum ScreenImageState
{
    None,
    Ready,
    NeedsCrop,
    MissingOrDamaged
}

public sealed class ScreenImageService
{
    private const int ManagedSourceMaxDimension = 4096;
    private readonly IAppPaths _paths;
    private readonly IUserSettings _settings;

    public ScreenImageService(IAppPaths paths, IUserSettings settings)
    {
        _paths = paths;
        _settings = settings;
        DeleteManagedFile(Path.Combine(_paths.SkinsDirectory, "keyboard-skin.png"));
    }

    public event Action? Changed;

    public ScreenImageState GetState(DisplayLayout layout)
    {
        if (string.IsNullOrWhiteSpace(_settings.ScreenImagePath)) return ScreenImageState.None;
        if (!File.Exists(_settings.ScreenImagePath) || !CanDecode(_settings.ScreenImagePath))
            return ScreenImageState.MissingOrDamaged;
        var aspect = layout.VirtualWidth / (double)Math.Max(1, layout.VirtualHeight);
        return _settings.ScreenImageCrop?.Matches(layout.Signature, aspect) == true
            ? ScreenImageState.Ready
            : ScreenImageState.NeedsCrop;
    }

    public BitmapSource LoadExternalSource(string path) => DecodeAndNormalize(path);

    public BitmapSource? LoadManagedSource()
    {
        var path = _settings.ScreenImagePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try { return DecodeAndNormalize(path); }
        catch { return null; }
    }

    public void Save(BitmapSource source, ScreenImageCropSettings crop)
    {
        if (!crop.IsValid) throw new InvalidDataException("裁剪参数无效。");
        Directory.CreateDirectory(_paths.SkinsDirectory);
        var destination = Path.Combine(_paths.SkinsDirectory, "screen-image-source.png");
        var staged = destination + ".staged";
        EncodePng(Normalize(source), staged);
        File.Move(staged, destination, true);

        var previous = _settings.ScreenImagePath;
        _settings.ScreenImagePath = destination;
        _settings.ScreenImageCrop = crop;
        _settings.Save();
        if (!string.Equals(previous, destination, StringComparison.OrdinalIgnoreCase) && IsManagedPath(previous))
            DeleteManagedFile(previous!);
        DeleteManagedFile(Path.Combine(_paths.SkinsDirectory, "screen-skin.png"));
        Changed?.Invoke();
    }

    public void Remove()
    {
        var previous = _settings.ScreenImagePath;
        _settings.ScreenImagePath = null;
        _settings.ScreenImageCrop = null;
        _settings.Save();
        if (IsManagedPath(previous)) DeleteManagedFile(previous!);
        DeleteManagedFile(Path.Combine(_paths.SkinsDirectory, "screen-skin.png"));
        Changed?.Invoke();
    }

    public bool TryLoadCroppedPixels(DisplayLayout layout, int width, int height, out byte[] pixels)
    {
        pixels = [];
        if (width <= 0 || height <= 0 || width > 2048 || height > 2048) return false;
        var crop = _settings.ScreenImageCrop;
        var aspect = layout.VirtualWidth / (double)Math.Max(1, layout.VirtualHeight);
        if (crop?.Matches(layout.Signature, aspect) != true) return false;
        var source = LoadManagedSource();
        if (source is null) return false;
        try
        {
            var x = Math.Clamp((int)Math.Round(crop.X * source.PixelWidth), 0, source.PixelWidth - 1);
            var y = Math.Clamp((int)Math.Round(crop.Y * source.PixelHeight), 0, source.PixelHeight - 1);
            var cropWidth = Math.Clamp((int)Math.Round(crop.Width * source.PixelWidth), 1, source.PixelWidth - x);
            var cropHeight = Math.Clamp((int)Math.Round(crop.Height * source.PixelHeight), 1, source.PixelHeight - y);
            BitmapSource transformed = new CroppedBitmap(source, new System.Windows.Int32Rect(x, y, cropWidth, cropHeight));
            transformed = new TransformedBitmap(transformed,
                new ScaleTransform(width / (double)cropWidth, height / (double)cropHeight));
            transformed = new FormatConvertedBitmap(transformed, PixelFormats.Bgra32, null, 0);
            pixels = new byte[width * height * 4];
            transformed.CopyPixels(pixels, width * 4, 0);
            return true;
        }
        catch
        {
            pixels = [];
            return false;
        }
    }

    private BitmapSource DecodeAndNormalize(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length <= 0 || info.Length > 100 * 1024 * 1024)
            throw new InvalidDataException("图片不存在、为空或文件过大。");
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0) throw new InvalidDataException("无法读取图片内容。");
        return Normalize(decoder.Frames[0]);
    }

    private static BitmapSource Normalize(BitmapSource source)
    {
        if (source.PixelWidth <= 0 || source.PixelHeight <= 0 ||
            (long)source.PixelWidth * source.PixelHeight > 120_000_000)
            throw new InvalidDataException("图片尺寸无效或过大。");
        var scale = Math.Min(1, ManagedSourceMaxDimension /
                                (double)Math.Max(source.PixelWidth, source.PixelHeight));
        BitmapSource normalized = scale < 1
            ? new TransformedBitmap(source, new ScaleTransform(scale, scale))
            : source;
        normalized = new FormatConvertedBitmap(normalized, PixelFormats.Bgra32, null, 0);
        normalized.Freeze();
        return normalized;
    }

    private static void EncodePng(BitmapSource source, string path)
    {
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    private bool IsManagedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var root = Path.GetFullPath(_paths.SkinsDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static bool CanDecode(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames.Count > 0;
        }
        catch { return false; }
    }

    private static void DeleteManagedFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }
}
