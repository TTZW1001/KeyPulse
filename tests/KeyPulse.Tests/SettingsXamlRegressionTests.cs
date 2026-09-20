using Xunit;

namespace KeyPulse.Tests;

public sealed class SettingsXamlRegressionTests
{
    [Fact]
    public void AutoBackupDirectory_ReadOnlyProperty_UsesOneWayBinding()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "KeyPulse.App", "Views", "SettingsView.xaml"));

        Assert.Contains("Text=\"{Binding AutoBackupDirectory, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void HeatmapAppearance_HasScreenCropControls_AndNoKeyboardImageControls()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "KeyPulse.App", "Views", "SettingsView.xaml"));
        var keyboardXaml = File.ReadAllText(Path.Combine(root, "src", "KeyPulse.App", "Views", "KeyboardView.xaml"));

        Assert.Contains("ImportScreenImageCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("RecropScreenImageCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("KeyboardSkin", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("KeyboardSkin", keyboardXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("KeyOpacity", keyboardXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MouseHeatmaps_UseScrollFriendlyRendering()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "KeyPulse.App", "Views", "MouseView.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "KeyPulse.App", "ViewModels", "MouseViewModel.cs"));

        Assert.True(xaml.Split("CacheMode=\"BitmapCache\"").Length - 1 >= 2,
            "The heatmap preview group and trend chart must be cached while scrolling.");
        Assert.Equal(3, xaml.Split("RenderOptions.BitmapScalingMode=\"LowQuality\"").Length - 1);
        Assert.Contains("PreviewHeatmapMaxDimension = 480", viewModel, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "KeyPulse.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("KeyPulse repository root was not found.");
    }
}
