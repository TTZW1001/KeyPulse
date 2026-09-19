using KeyPulse.Core;
using Media = System.Windows.Media;

namespace KeyPulse.App.Services;

public static class HeatmapPaletteService
{
    public static Media.Color Accent(HeatmapPalette palette) => palette switch
    {
        HeatmapPalette.Ember => Media.Color.FromRgb(0xD1, 0x62, 0x4B),
        HeatmapPalette.Forest => Media.Color.FromRgb(0x3E, 0x8B, 0x67),
        HeatmapPalette.Violet => Media.Color.FromRgb(0x86, 0x64, 0xB8),
        _ => Media.Color.FromRgb(0x4E, 0x6E, 0x9E)
    };
}
