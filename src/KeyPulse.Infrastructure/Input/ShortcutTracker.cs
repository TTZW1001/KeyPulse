using KeyPulse.Core.Events;

namespace KeyPulse.Infrastructure.Input;

/// <summary>
/// Tracks modifier state in memory and emits aggregate-safe shortcut names.
/// It never stores event order beyond the currently held modifiers.
/// </summary>
public sealed class ShortcutTracker
{
    private readonly HashSet<string> _heldModifiers = new(StringComparer.Ordinal);

    public string? Process(KeyPressedEvent input)
    {
        var modifier = NormalizeModifier(input.Key.Name);
        if (modifier is not null)
        {
            if (input.IsKeyDown)
            {
                _heldModifiers.Add(modifier);
            }
            else
            {
                _heldModifiers.Remove(modifier);
            }

            return null;
        }

        if (!input.IsKeyDown || input.Key.Name is "Unknown" or "")
        {
            return null;
        }

        var hasCtrl = _heldModifiers.Contains("Ctrl");
        var hasAlt = _heldModifiers.Contains("Alt");
        var hasWin = _heldModifiers.Contains("Win");
        if (!hasCtrl && !hasAlt && !hasWin)
        {
            return null;
        }

        var parts = new List<string>(5);
        if (hasCtrl) parts.Add("Ctrl");
        if (_heldModifiers.Contains("Shift")) parts.Add("Shift");
        if (hasAlt) parts.Add("Alt");
        if (hasWin) parts.Add("Win");
        parts.Add(input.Key.Name);
        return string.Join('+', parts);
    }

    public void Reset() => _heldModifiers.Clear();

    private static string? NormalizeModifier(string key) => key switch
    {
        "LeftCtrl" or "RightCtrl" => "Ctrl",
        "LeftShift" or "RightShift" => "Shift",
        "LeftAlt" or "RightAlt" => "Alt",
        "LeftWin" or "RightWin" => "Win",
        _ => null
    };
}
