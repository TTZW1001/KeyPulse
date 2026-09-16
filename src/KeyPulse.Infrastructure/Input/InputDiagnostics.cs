using KeyPulse.Core.Events;
using KeyPulse.Core.Models;

namespace KeyPulse.Infrastructure.Input;

/// <summary>
/// Keeps a short, opt-in keyboard packet trace in memory only. Entries are never logged or persisted.
/// </summary>
public sealed class InputDiagnostics
{
    private const int Capacity = 16;
    private readonly object _gate = new();
    private readonly Queue<string> _entries = new(Capacity);
    private bool _enabled;

    public event Action? Changed;

    public bool Enabled
    {
        get
        {
            lock (_gate)
            {
                return _enabled;
            }
        }
    }

    public void SetEnabled(bool enabled)
    {
        lock (_gate)
        {
            _enabled = enabled;
            _entries.Clear();
        }

        NotifyChanged();
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }

        NotifyChanged();
    }

    public IReadOnlyList<string> Snapshot()
    {
        lock (_gate)
        {
            return _entries.Reverse().ToArray();
        }
    }

    public void RecordKeyboard(
        DateTimeOffset timestamp,
        ushort virtualKey,
        ushort scanCode,
        ushort flags,
        KeyboardModifiers observedModifiers,
        KeyPressedEvent? parsed)
    {
        lock (_gate)
        {
            if (!_enabled)
            {
                return;
            }

            var direction = (flags & RawKeyboardParser.KeyBreak) == 0 ? "按下" : "松开";
            var key = parsed?.Key.Name ?? "已忽略";
            var recovered = parsed?.RecoveredFromScanCode == true ? " · 扫描码恢复" : string.Empty;
            var modifiers = FormatModifiers(observedModifiers);
            _entries.Enqueue(
                $"{timestamp:HH:mm:ss.fff}  VK {virtualKey:X2}  SC {scanCode:X2}  {direction}  {key}{recovered}  系统修饰键：{modifiers}");
            while (_entries.Count > Capacity)
            {
                _entries.Dequeue();
            }
        }

        NotifyChanged();
    }

    public void RecordShortcutDecision(KeyPressedEvent input, string? shortcut)
    {
        if (!input.IsKeyDown || IsModifier(input.Key.Name))
        {
            return;
        }

        lock (_gate)
        {
            if (!_enabled)
            {
                return;
            }

            _entries.Enqueue(
                $"{input.Timestamp:HH:mm:ss.fff}  组合判定：{(shortcut ?? "未形成快捷键")}  末键：{input.Key.Name}");
            while (_entries.Count > Capacity)
            {
                _entries.Dequeue();
            }
        }

        NotifyChanged();
    }

    private static string FormatModifiers(KeyboardModifiers modifiers)
    {
        if (modifiers == KeyboardModifiers.None)
        {
            return "无";
        }

        var names = new List<string>(4);
        if ((modifiers & KeyboardModifiers.Ctrl) != 0) names.Add("Ctrl");
        if ((modifiers & KeyboardModifiers.Shift) != 0) names.Add("Shift");
        if ((modifiers & KeyboardModifiers.Alt) != 0) names.Add("Alt");
        if ((modifiers & KeyboardModifiers.Win) != 0) names.Add("Win");
        return string.Join('+', names);
    }

    private static bool IsModifier(string key) => key is
        "LeftCtrl" or "RightCtrl" or
        "LeftShift" or "RightShift" or
        "LeftAlt" or "RightAlt" or
        "LeftWin" or "RightWin";

    private void NotifyChanged()
    {
        var handlers = Changed;
        if (handlers is null)
        {
            return;
        }

        foreach (Action handler in handlers.GetInvocationList())
        {
            try
            {
                handler();
            }
            catch
            {
                // Diagnostics must never interrupt input capture.
            }
        }
    }
}
