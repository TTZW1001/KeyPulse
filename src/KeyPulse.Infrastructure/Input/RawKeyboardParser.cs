using KeyPulse.Core.Events;

namespace KeyPulse.Infrastructure.Input;

public sealed class RawKeyboardParser
{
    public const ushort KeyBreak = RawInputNativeMethods.RI_KEY_BREAK;

    public KeyPressedEvent TryParse(ushort virtualKey, ushort flags, ushort makeCode, DateTimeOffset timestamp)
    {
        return new KeyPressedEvent
        {
            Timestamp = timestamp,
            Key = KeyMapper.Map(virtualKey, flags, makeCode),
            IsKeyDown = (flags & KeyBreak) == 0
        };
    }
}
