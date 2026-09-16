using KeyPulse.Core.Events;
using KeyPulse.Core.Models;

namespace KeyPulse.Infrastructure.Input;

/// <summary>
/// Tracks modifier state in memory and emits aggregate-safe shortcut names.
/// It retains only held modifiers and a short modifier timestamp for recovered global-hotkey packets.
/// </summary>
public sealed class ShortcutTracker
{
    private static readonly TimeSpan RecoveredModifierGrace = TimeSpan.FromMilliseconds(250);
    private readonly HashSet<string> _heldModifiers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pressedKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _lastModifierActivity = new(StringComparer.Ordinal);

    public string? Process(KeyPressedEvent input)
    {
        var modifier = NormalizeModifier(input.Key.Name);
        if (modifier is not null)
        {
            _lastModifierActivity[modifier] = input.Timestamp;
            if (input.IsKeyDown)
            {
                if (_heldModifiers.Count == 0)
                {
                    // A new modifier chord invalidates unmatched ordinary key-down state.
                    // This lets a later key-up recover a hotkey packet whose key-down was
                    // consumed by another global-hook application.
                    _pressedKeys.Clear();
                }

                _heldModifiers.Add(modifier);
            }
            else
            {
                _heldModifiers.Remove(modifier);
            }

            return null;
        }

        if (input.Key.Name is "Unknown" or "")
        {
            return null;
        }

        if (input.IsKeyDown)
        {
            _pressedKeys.Add(input.Key.Name);
        }
        else if (_pressedKeys.Remove(input.Key.Name))
        {
            // The normal key-down already produced the shortcut decision.
            return null;
        }

        var hasCtrl = HasModifier("Ctrl", KeyboardModifiers.Ctrl, input);
        var hasAlt = HasModifier("Alt", KeyboardModifiers.Alt, input);
        var hasWin = HasModifier("Win", KeyboardModifiers.Win, input);
        if (!hasCtrl && !hasAlt && !hasWin)
        {
            return null;
        }

        var parts = new List<string>(5);
        if (hasCtrl) parts.Add("Ctrl");
        if (HasModifier("Shift", KeyboardModifiers.Shift, input)) parts.Add("Shift");
        if (hasAlt) parts.Add("Alt");
        if (hasWin) parts.Add("Win");
        parts.Add(input.Key.Name);
        return string.Join('+', parts);
    }

    public void Reset()
    {
        _heldModifiers.Clear();
        _pressedKeys.Clear();
        _lastModifierActivity.Clear();
    }

    private bool HasModifier(string name, KeyboardModifiers observed, KeyPressedEvent input)
    {
        if (_heldModifiers.Contains(name) || (input.ObservedModifiers & observed) != 0)
        {
            return true;
        }

        return input.RecoveredFromScanCode &&
               _lastModifierActivity.TryGetValue(name, out var lastActivity) &&
               input.Timestamp >= lastActivity &&
               input.Timestamp - lastActivity <= RecoveredModifierGrace;
    }

    private static string? NormalizeModifier(string key) => key switch
    {
        "LeftCtrl" or "RightCtrl" => "Ctrl",
        "LeftShift" or "RightShift" => "Shift",
        "LeftAlt" or "RightAlt" => "Alt",
        "LeftWin" or "RightWin" => "Win",
        _ => null
    };
}
