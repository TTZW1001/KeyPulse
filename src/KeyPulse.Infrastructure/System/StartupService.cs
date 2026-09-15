using System.Diagnostics;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;

namespace KeyPulse.Infrastructure.System;

public sealed class StartupService : IStartupService
{
    public const string DefaultValueName = "KeyPulse";

    private readonly IRunKeyStore _runKeyStore;
    private readonly string _executablePath;
    private readonly string _valueName;

    public StartupService(IRunKeyStore runKeyStore)
        : this(runKeyStore, ResolveExecutablePath(), DefaultValueName)
    {
    }

    public StartupService(IRunKeyStore runKeyStore, string executablePath, string valueName)
    {
        _runKeyStore = runKeyStore;
        _executablePath = executablePath;
        _valueName = valueName;
        CommandLine = "\"" + _executablePath + "\" " + LaunchArguments.StartupFlag;
    }

    public string CommandLine { get; }

    public bool IsEnabled =>
        string.Equals(_runKeyStore.GetValue(_valueName), CommandLine, StringComparison.OrdinalIgnoreCase);

    public void Enable() => _runKeyStore.SetValue(_valueName, CommandLine);

    public void Disable() => _runKeyStore.DeleteValue(_valueName);

    private static string ResolveExecutablePath()
    {
        return Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? AppContext.BaseDirectory;
    }
}
