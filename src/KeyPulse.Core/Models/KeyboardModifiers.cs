namespace KeyPulse.Core.Models;

[Flags]
public enum KeyboardModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Win = 8
}
