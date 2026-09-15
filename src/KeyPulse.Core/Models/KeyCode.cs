namespace KeyPulse.Core.Models;

public readonly record struct KeyCode(string Name)
{
    public static KeyCode Unknown { get; } = new("Unknown");

    public override string ToString() => Name;
}
