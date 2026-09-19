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
