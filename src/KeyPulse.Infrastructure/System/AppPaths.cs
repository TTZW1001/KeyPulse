using KeyPulse.Core.Interfaces;

namespace KeyPulse.Infrastructure.System;

public sealed class AppPaths : IAppPaths
{
    public AppPaths()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KeyPulse"))
    {
    }

    public AppPaths(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        LogsDirectory = Path.Combine(RootDirectory, "logs");
        DataDirectory = Path.Combine(RootDirectory, "data");
        DatabasePath = Path.Combine(DataDirectory, "keypulse.db");
        ConfigDirectory = Path.Combine(RootDirectory, "config");
        SettingsPath = Path.Combine(ConfigDirectory, "settings.json");
        ExcludedAppsPath = Path.Combine(ConfigDirectory, "excluded-apps.json");
        BackupsDirectory = Path.Combine(RootDirectory, "backups");
        SkinsDirectory = Path.Combine(RootDirectory, "skins");
    }

    public string RootDirectory { get; }
    public string LogsDirectory { get; }
    public string DataDirectory { get; }
    public string DatabasePath { get; }
    public string ConfigDirectory { get; }
    public string SettingsPath { get; }
    public string ExcludedAppsPath { get; }
    public string BackupsDirectory { get; }
    public string SkinsDirectory { get; }
}
