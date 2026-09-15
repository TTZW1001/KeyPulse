using Microsoft.Win32;

namespace KeyPulse.Infrastructure.System;

public sealed class WindowsRunKeyStore : IRunKeyStore
{
    public const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? GetValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: false);
        return key?.GetValue(name) as string;
    }

    public void SetValue(string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunSubKey, writable: true);
        key.SetValue(name, value);
    }

    public void DeleteValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}
