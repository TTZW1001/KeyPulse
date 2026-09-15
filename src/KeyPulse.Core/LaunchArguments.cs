namespace KeyPulse.Core;

public static class LaunchArguments
{
    public const string StartupFlag = "--startup";

    public static bool IsSilentStartup(IEnumerable<string> args) =>
        args.Any(argument => string.Equals(argument, StartupFlag, StringComparison.OrdinalIgnoreCase));
}
