using KeyPulse.Core.Events;
using KeyPulse.Core.Models;

namespace KeyPulse.Infrastructure.Input;

public sealed class RawKeyboardParser
{
    public const ushort KeyBreak = RawInputNativeMethods.RI_KEY_BREAK;
    private const ushort KeyboardOverrunMakeCode = 0xFF;
    private const ushort UnmappedVirtualKey = 0xFF;

    public KeyPressedEvent? TryParse(
        ushort virtualKey,
        ushort flags,
        ushort makeCode,
        DateTimeOffset timestamp,
        KeyboardModifiers observedModifiers = KeyboardModifiers.None)
    {
        if (makeCode == KeyboardOverrunMakeCode || virtualKey > UnmappedVirtualKey)
        {
            return null;
        }

        // Global-hotkey tools can mask the final key with VK_FF while preserving its
        // physical scan code. Recover that key for aggregate shortcut statistics, but
        // never persist an unresolvable VK_FF/Unknown bucket.
        var key = virtualKey >= UnmappedVirtualKey
            ? KeyMapper.MapScanCode(makeCode, flags)
            : KeyMapper.Map(virtualKey, flags, makeCode);
        if (key == KeyPulse.Core.Models.KeyCode.Unknown)
        {
            return null;
        }

        return new KeyPressedEvent
        {
            Timestamp = timestamp,
            Key = key,
            IsKeyDown = (flags & KeyBreak) == 0,
            ObservedModifiers = observedModifiers,
            RecoveredFromScanCode = virtualKey == UnmappedVirtualKey,
            VirtualKey = virtualKey,
            ScanCode = makeCode,
            RawFlags = flags
        };
    }
}
