namespace KeyPulse.Core.Interfaces;

public interface IAppPaths
{
    string RootDirectory { get; }
    string LogsDirectory { get; }
    string DataDirectory { get; }
    string DatabasePath { get; }
}
