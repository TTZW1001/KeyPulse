using KeyPulse.Core.Models;

namespace KeyPulse.Infrastructure.Input;

internal static class WindowsKeyboardState
{
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLeftWin = 0x5B;
    private const int VkRightWin = 0x5C;

    public static KeyboardModifiers Capture()
    {
        var result = KeyboardModifiers.None;
        if (IsDown(VkControl)) result |= KeyboardModifiers.Ctrl;
        if (IsDown(VkShift)) result |= KeyboardModifiers.Shift;
        if (IsDown(VkMenu)) result |= KeyboardModifiers.Alt;
        if (IsDown(VkLeftWin) || IsDown(VkRightWin)) result |= KeyboardModifiers.Win;
        return result;
    }

    private static bool IsDown(int virtualKey) =>
        (RawInputNativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
}
