namespace KeyPulse.Core;

public static class DefaultExcludedApps
{
    public static readonly string[] Names =
    [
        "LockApp.exe",
        "LogonUI.exe"
    ];

    private static readonly HashSet<string> Set = new(Names, StringComparer.OrdinalIgnoreCase);

    public static bool Contains(string processName) => Set.Contains(processName);
}
