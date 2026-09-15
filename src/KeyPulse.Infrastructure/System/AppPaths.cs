using KeyPulse.Core.Interfaces;

namespace KeyPulse.Infrastructure.System;

public sealed class AppPaths : IAppPaths
{
    public AppPaths()
    {
        RootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KeyPulse");
        LogsDirectory = Path.Combine(RootDirectory, "logs");
    }

    public string RootDirectory { get; }
    public string LogsDirectory { get; }
}
