using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Models;
using Xunit;

namespace KeyPulse.Tests;

public sealed class HeatRevealMaskTests
{
    private static readonly DisplayMonitor Monitor = new("m", 0, 0, 100, 100, 96, 96, true);
    private static readonly DisplayLayout Layout = new("layout", 0, 0, 100, 100, [Monitor]);

    [Fact]
    public void CropSettings_RejectInvalidBounds_AndMatchLayoutAspect()
    {
        var valid = new ScreenImageCropSettings(0.1, 0.2, 0.8, 0.6, 1.5, "layout");
        Assert.True(valid.IsValid);
        Assert.True(valid.Matches("layout", 1.5));
        Assert.False(valid.Matches("changed", 1.5));
        Assert.False(new ScreenImageCropSettings(0.8, 0, 0.4, 1, 1, "layout").IsValid);
    }

    [Fact]
    public void DensityMask_IsSoftAndStrongerAtFrequentlyUsedArea()
    {
        var cells = new uint[100];
        cells[(5 * 10) + 5] = 100;
        var result = Result([new PointerDensityGrid("m", 10, 10, cells)], []);

        var mask = HeatRevealMaskBuilder.Build(result, 100, 100);

        Assert.True(mask[(55 * 100) + 55] > mask[(5 * 100) + 5]);
        Assert.True(mask[(55 * 100) + 55] > mask[(55 * 100) + 75]);
        Assert.True(mask[(55 * 100) + 62] > 0); // Smoothing extends beyond the original 10-pixel cell.
    }

    [Fact]
    public void Clicks_AddLocalBoost_AndRespectButtonFilter()
    {
        var density = new PointerDensityGrid("m", 10, 10, new uint[100]);
        var result = Result([density],
        [
            new PointerClickPoint("m", 20, 20, 50, "Left"),
            new PointerClickPoint("m", 80, 80, 50, "Right")
        ]);

        var left = HeatRevealMaskBuilder.Build(result, 100, 100, buttonFilter: "Left");
        var right = HeatRevealMaskBuilder.Build(result, 100, 100, buttonFilter: "Right");

        Assert.True(left[(20 * 100) + 20] > left[(80 * 100) + 80]);
        Assert.True(right[(80 * 100) + 80] > right[(20 * 100) + 20]);
    }

    private static PointerHeatmapResult Result(
        IReadOnlyList<PointerDensityGrid> densities,
        IReadOnlyList<PointerClickPoint> clicks) =>
        new(Layout, clicks, densities, [], 0, 10_000);
}
