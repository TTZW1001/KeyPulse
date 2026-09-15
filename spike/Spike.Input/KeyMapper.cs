namespace Spike.Input;

/// <summary>
/// Maps Raw Input virtual keys to stable KeyPulse-style names.
/// Intentionally does not call ToUnicode / ToUnicodeEx and does not read IME text.
/// </summary>
internal static class KeyMapper
{
    public static string Map(ushort virtualKey, ushort flags, ushort makeCode)
    {
        var extended = (flags & NativeMethods.RI_KEY_E0) != 0;

        return virtualKey switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x10 => makeCode == 0x36 ? "RightShift" : "LeftShift",
            0x11 => extended ? "RightCtrl" : "LeftCtrl",
            0x12 => extended ? "RightAlt" : "LeftAlt",
            0x13 => "Pause",
            0x14 => "CapsLock",
            0x1B => "Escape",
            0x20 => "Space",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "ArrowLeft",
            0x26 => "ArrowUp",
            0x27 => "ArrowRight",
            0x28 => "ArrowDown",
            0x2C => "PrintScreen",
            0x2D => "Insert",
            0x2E => "Delete",
            0x5B => "LeftWin",
            0x5C => "RightWin",
            0x5D => "Menu",
            0x90 => "NumLock",
            0x91 => "ScrollLock",
            0xA0 => "LeftShift",
            0xA1 => "RightShift",
            0xA2 => "LeftCtrl",
            0xA3 => "RightCtrl",
            0xA4 => "LeftAlt",
            0xA5 => "RightAlt",
            0xAD => "VolumeMute",
            0xAE => "VolumeDown",
            0xAF => "VolumeUp",
            >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
            >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
            >= 0x60 and <= 0x69 => "NumPad" + (virtualKey - 0x60),
            0x6A => "NumPadMultiply",
            0x6B => "NumPadAdd",
            0x6C => "NumPadSeparator",
            0x6D => "NumPadSubtract",
            0x6E => "NumPadDecimal",
            0x6F => "NumPadDivide",
            >= 0x70 and <= 0x7B => "F" + (virtualKey - 0x6F),
            0xBA => "Oem1",
            0xBB => "OemPlus",
            0xBC => "OemComma",
            0xBD => "OemMinus",
            0xBE => "OemPeriod",
            0xBF => "Oem2",
            0xC0 => "Oem3",
            0xDB => "Oem4",
            0xDC => "Oem5",
            0xDD => "Oem6",
            0xDE => "Oem7",
            _ => "VK_" + virtualKey.ToString("X2")
        };
    }
}
