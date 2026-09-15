using KeyPulse.Core.Models;

namespace KeyPulse.Infrastructure.Input;

/// <summary>
/// Maps Raw Input virtual keys / scan codes to stable KeyPulse names.
/// Does not call ToUnicode / ToUnicodeEx and does not read IME text.
/// </summary>
public static class KeyMapper
{
    public const ushort VirtualKeyProcessKey = 0xE5;

    public static KeyCode Map(ushort virtualKey, ushort flags, ushort makeCode)
    {
        if (virtualKey == 0 || virtualKey == VirtualKeyProcessKey)
        {
            return MapScanCode(makeCode, flags);
        }

        return MapVirtualKey(virtualKey, flags, makeCode);
    }

    public static KeyCode MapVirtualKey(ushort virtualKey, ushort flags, ushort makeCode)
    {
        var extended = (flags & RawInputNativeMethods.RI_KEY_E0) != 0;

        return virtualKey switch
        {
            0x08 => Named("Backspace"),
            0x09 => Named("Tab"),
            0x0D => Named("Enter"),
            0x10 => Named(makeCode == 0x36 ? "RightShift" : "LeftShift"),
            0x11 => Named(extended ? "RightCtrl" : "LeftCtrl"),
            0x12 => Named(extended ? "RightAlt" : "LeftAlt"),
            0x13 => Named("Pause"),
            0x14 => Named("CapsLock"),
            0x1B => Named("Escape"),
            0x20 => Named("Space"),
            0x21 => Named("PageUp"),
            0x22 => Named("PageDown"),
            0x23 => Named("End"),
            0x24 => Named("Home"),
            0x25 => Named("ArrowLeft"),
            0x26 => Named("ArrowUp"),
            0x27 => Named("ArrowRight"),
            0x28 => Named("ArrowDown"),
            0x2C => Named("PrintScreen"),
            0x2D => Named("Insert"),
            0x2E => Named("Delete"),
            0x5B => Named("LeftWin"),
            0x5C => Named("RightWin"),
            0x5D => Named("Menu"),
            0x90 => Named("NumLock"),
            0x91 => Named("ScrollLock"),
            0xA0 => Named("LeftShift"),
            0xA1 => Named("RightShift"),
            0xA2 => Named("LeftCtrl"),
            0xA3 => Named("RightCtrl"),
            0xA4 => Named("LeftAlt"),
            0xA5 => Named("RightAlt"),
            0xAD => Named("VolumeMute"),
            0xAE => Named("VolumeDown"),
            0xAF => Named("VolumeUp"),
            >= 0x30 and <= 0x39 => Named(((char)virtualKey).ToString()),
            >= 0x41 and <= 0x5A => Named(((char)virtualKey).ToString()),
            >= 0x60 and <= 0x69 => Named("NumPad" + (virtualKey - 0x60)),
            0x6A => Named("NumPadMultiply"),
            0x6B => Named("NumPadAdd"),
            0x6C => Named("NumPadSeparator"),
            0x6D => Named("NumPadSubtract"),
            0x6E => Named("NumPadDecimal"),
            0x6F => Named("NumPadDivide"),
            >= 0x70 and <= 0x7B => Named("F" + (virtualKey - 0x6F)),
            0x7C => Named("F13"),
            0x7D => Named("F14"),
            0x7E => Named("F15"),
            0x7F => Named("F16"),
            0x80 => Named("F17"),
            0x81 => Named("F18"),
            0x82 => Named("F19"),
            0x83 => Named("F20"),
            0x84 => Named("F21"),
            0x85 => Named("F22"),
            0x86 => Named("F23"),
            0x87 => Named("F24"),
            0xBA => Named("Oem1"),
            0xBB => Named("OemPlus"),
            0xBC => Named("OemComma"),
            0xBD => Named("OemMinus"),
            0xBE => Named("OemPeriod"),
            0xBF => Named("Oem2"),
            0xC0 => Named("Oem3"),
            0xDB => Named("Oem4"),
            0xDC => Named("Oem5"),
            0xDD => Named("Oem6"),
            0xDE => Named("Oem7"),
            0xE2 => Named("Oem102"),
            _ => Named("VK_" + virtualKey.ToString("X2"))
        };
    }

    public static KeyCode MapScanCode(ushort makeCode, ushort flags)
    {
        var extended = (flags & RawInputNativeMethods.RI_KEY_E0) != 0;
        if (extended)
        {
            return makeCode switch
            {
                0x1C => Named("Enter"),
                0x1D => Named("RightCtrl"),
                0x35 => Named("NumPadDivide"),
                0x37 => Named("PrintScreen"),
                0x38 => Named("RightAlt"),
                0x47 => Named("Home"),
                0x48 => Named("ArrowUp"),
                0x49 => Named("PageUp"),
                0x4B => Named("ArrowLeft"),
                0x4D => Named("ArrowRight"),
                0x4F => Named("End"),
                0x50 => Named("ArrowDown"),
                0x51 => Named("PageDown"),
                0x52 => Named("Insert"),
                0x53 => Named("Delete"),
                0x5B => Named("LeftWin"),
                0x5C => Named("RightWin"),
                0x5D => Named("Menu"),
                _ => KeyCode.Unknown
            };
        }

        return makeCode switch
        {
            0x01 => Named("Escape"),
            0x02 => Named("1"),
            0x03 => Named("2"),
            0x04 => Named("3"),
            0x05 => Named("4"),
            0x06 => Named("5"),
            0x07 => Named("6"),
            0x08 => Named("7"),
            0x09 => Named("8"),
            0x0A => Named("9"),
            0x0B => Named("0"),
            0x0C => Named("OemMinus"),
            0x0D => Named("OemPlus"),
            0x0E => Named("Backspace"),
            0x0F => Named("Tab"),
            0x10 => Named("Q"),
            0x11 => Named("W"),
            0x12 => Named("E"),
            0x13 => Named("R"),
            0x14 => Named("T"),
            0x15 => Named("Y"),
            0x16 => Named("U"),
            0x17 => Named("I"),
            0x18 => Named("O"),
            0x19 => Named("P"),
            0x1A => Named("Oem4"),
            0x1B => Named("Oem6"),
            0x1C => Named("Enter"),
            0x1D => Named("LeftCtrl"),
            0x1E => Named("A"),
            0x1F => Named("S"),
            0x20 => Named("D"),
            0x21 => Named("F"),
            0x22 => Named("G"),
            0x23 => Named("H"),
            0x24 => Named("J"),
            0x25 => Named("K"),
            0x26 => Named("L"),
            0x27 => Named("Oem1"),
            0x28 => Named("Oem7"),
            0x29 => Named("Oem3"),
            0x2A => Named("LeftShift"),
            0x2B => Named("Oem5"),
            0x2C => Named("Z"),
            0x2D => Named("X"),
            0x2E => Named("C"),
            0x2F => Named("V"),
            0x30 => Named("B"),
            0x31 => Named("N"),
            0x32 => Named("M"),
            0x33 => Named("OemComma"),
            0x34 => Named("OemPeriod"),
            0x35 => Named("Oem2"),
            0x36 => Named("RightShift"),
            0x37 => Named("NumPadMultiply"),
            0x38 => Named("LeftAlt"),
            0x39 => Named("Space"),
            0x3A => Named("CapsLock"),
            0x3B => Named("F1"),
            0x3C => Named("F2"),
            0x3D => Named("F3"),
            0x3E => Named("F4"),
            0x3F => Named("F5"),
            0x40 => Named("F6"),
            0x41 => Named("F7"),
            0x42 => Named("F8"),
            0x43 => Named("F9"),
            0x44 => Named("F10"),
            0x57 => Named("F11"),
            0x58 => Named("F12"),
            0x47 => Named("NumPad7"),
            0x48 => Named("NumPad8"),
            0x49 => Named("NumPad9"),
            0x4A => Named("NumPadSubtract"),
            0x4B => Named("NumPad4"),
            0x4C => Named("NumPad5"),
            0x4D => Named("NumPad6"),
            0x4E => Named("NumPadAdd"),
            0x4F => Named("NumPad1"),
            0x50 => Named("NumPad2"),
            0x51 => Named("NumPad3"),
            0x52 => Named("NumPad0"),
            0x53 => Named("NumPadDecimal"),
            _ => KeyCode.Unknown
        };
    }

    private static KeyCode Named(string name) => new(name);
}
