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
    }

    public string RootDirectory { get; }
    public string LogsDirectory { get; }
    public string DataDirectory { get; }
    public string DatabasePath { get; }
}
