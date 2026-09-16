namespace KeyPulse.Core.Models;

public readonly record struct KeyCode(string Name)
{
    public static KeyCode Unknown { get; } = new("Unknown");

    public static bool IsValidStatisticName(string name) =>
        name is not "Unknown" and not "VK_FF";

    public override string ToString() => Name;
}
