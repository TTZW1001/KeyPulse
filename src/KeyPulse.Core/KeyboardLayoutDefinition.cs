namespace KeyPulse.Core;

public sealed record KeyboardKeyDefinition(
    string KeyCode,
    string Label,
    int Row,
    double X,
    double Y,
    double Width,
    double Height = 1);

public static class KeyboardLayoutDefinition
{
    public const int KeyCount = 104;

    private const double NavX = 15.5;
    private const double NumX = 18.75;
    private const double RowGap = 0.4;

    public static IReadOnlyList<KeyboardKeyDefinition> Keys { get; } = Build();

    public static IReadOnlySet<string> MappedKeyCodes { get; } =
        Keys.Select(key => key.KeyCode).ToHashSet(StringComparer.Ordinal);

    private static IReadOnlyList<KeyboardKeyDefinition> Build()
    {
        var keys = new List<KeyboardKeyDefinition>(KeyCount);
        var y0 = 0.0;
        var y1 = 1.0 + RowGap;
        var y2 = y1 + 1.0;
        var y3 = y2 + 1.0;
        var y4 = y3 + 1.0;
        var y5 = y4 + 1.0;

        Add(keys, 0, y0,
            ("Escape", "Esc", 1),
            null,
            ("F1", "F1", 1), ("F2", "F2", 1), ("F3", "F3", 1), ("F4", "F4", 1),
            null,
            ("F5", "F5", 1), ("F6", "F6", 1), ("F7", "F7", 1), ("F8", "F8", 1),
            null,
            ("F9", "F9", 1), ("F10", "F10", 1), ("F11", "F11", 1), ("F12", "F12", 1));
        keys.Add(K("PrintScreen", "PrtSc", 0, NavX, y0, 1));
        keys.Add(K("ScrollLock", "ScrLk", 0, NavX + 1, y0, 1));
        keys.Add(K("Pause", "Pause", 0, NavX + 2, y0, 1));

        Add(keys, 1, y1,
            ("Oem3", "`", 1),
            ("1", "1", 1), ("2", "2", 1), ("3", "3", 1), ("4", "4", 1), ("5", "5", 1),
            ("6", "6", 1), ("7", "7", 1), ("8", "8", 1), ("9", "9", 1), ("0", "0", 1),
            ("OemMinus", "-", 1), ("OemPlus", "=", 1), ("Backspace", "Back", 2));
        keys.Add(K("Insert", "Ins", 1, NavX, y1, 1));
        keys.Add(K("Home", "Home", 1, NavX + 1, y1, 1));
        keys.Add(K("PageUp", "PgUp", 1, NavX + 2, y1, 1));
        keys.Add(K("NumLock", "Num", 1, NumX, y1, 1));
        keys.Add(K("NumPadDivide", "/", 1, NumX + 1, y1, 1));
        keys.Add(K("NumPadMultiply", "*", 1, NumX + 2, y1, 1));
        keys.Add(K("NumPadSubtract", "-", 1, NumX + 3, y1, 1));

        Add(keys, 2, y2,
            ("Tab", "Tab", 1.5),
            ("Q", "Q", 1), ("W", "W", 1), ("E", "E", 1), ("R", "R", 1), ("T", "T", 1),
            ("Y", "Y", 1), ("U", "U", 1), ("I", "I", 1), ("O", "O", 1), ("P", "P", 1),
            ("Oem4", "[", 1), ("Oem6", "]", 1), ("Oem5", "\\", 1.5));
        keys.Add(K("Delete", "Del", 2, NavX, y2, 1));
        keys.Add(K("End", "End", 2, NavX + 1, y2, 1));
        keys.Add(K("PageDown", "PgDn", 2, NavX + 2, y2, 1));
        keys.Add(K("NumPad7", "7", 2, NumX, y2, 1));
        keys.Add(K("NumPad8", "8", 2, NumX + 1, y2, 1));
        keys.Add(K("NumPad9", "9", 2, NumX + 2, y2, 1));
        keys.Add(K("NumPadAdd", "+", 2, NumX + 3, y2, 1, 2));

        Add(keys, 3, y3,
            ("CapsLock", "Caps", 1.75),
            ("A", "A", 1), ("S", "S", 1), ("D", "D", 1), ("F", "F", 1), ("G", "G", 1),
            ("H", "H", 1), ("J", "J", 1), ("K", "K", 1), ("L", "L", 1),
            ("Oem1", ";", 1), ("Oem7", "'", 1), ("Enter", "Enter", 2.25));
        keys.Add(K("NumPad4", "4", 3, NumX, y3, 1));
        keys.Add(K("NumPad5", "5", 3, NumX + 1, y3, 1));
        keys.Add(K("NumPad6", "6", 3, NumX + 2, y3, 1));

        Add(keys, 4, y4,
            ("LeftShift", "Shift", 2.25),
            ("Z", "Z", 1), ("X", "X", 1), ("C", "C", 1), ("V", "V", 1), ("B", "B", 1),
            ("N", "N", 1), ("M", "M", 1),
            ("OemComma", ",", 1), ("OemPeriod", ".", 1), ("Oem2", "/", 1),
            ("RightShift", "Shift", 2.75));
        keys.Add(K("ArrowUp", "↑", 4, NavX + 1, y4, 1));
        keys.Add(K("NumPad1", "1", 4, NumX, y4, 1));
        keys.Add(K("NumPad2", "2", 4, NumX + 1, y4, 1));
        keys.Add(K("NumPad3", "3", 4, NumX + 2, y4, 1));
        keys.Add(K("Enter", "Enter", 4, NumX + 3, y4, 1, 2));

        Add(keys, 5, y5,
            ("LeftCtrl", "Ctrl", 1.25),
            ("LeftWin", "Win", 1.25),
            ("LeftAlt", "Alt", 1.25),
            ("Space", "Space", 6.25),
            ("RightAlt", "Alt", 1.25),
            ("RightWin", "Win", 1.25),
            ("Menu", "Menu", 1.25),
            ("RightCtrl", "Ctrl", 1.25));
        keys.Add(K("ArrowLeft", "←", 5, NavX, y5, 1));
        keys.Add(K("ArrowDown", "↓", 5, NavX + 1, y5, 1));
        keys.Add(K("ArrowRight", "→", 5, NavX + 2, y5, 1));
        keys.Add(K("NumPad0", "0", 5, NumX, y5, 2));
        keys.Add(K("NumPadDecimal", ".", 5, NumX + 2, y5, 1));

        return keys;
    }

    private static void Add(
        List<KeyboardKeyDefinition> keys,
        int row,
        double y,
        params (string Code, string Label, double Width)?[] cells)
    {
        var x = 0.0;
        foreach (var cell in cells)
        {
            if (cell is null)
            {
                x += 0.5;
                continue;
            }

            keys.Add(K(cell.Value.Code, cell.Value.Label, row, x, y, cell.Value.Width));
            x += cell.Value.Width;
        }
    }

    private static KeyboardKeyDefinition K(
        string code,
        string label,
        int row,
        double x,
        double y,
        double width,
        double height = 1) =>
        new(code, label, row, x, y, width, height);
}
