using KeyPulse.Core.Events;
using KeyPulse.Core.Models;
using KeyPulse.Infrastructure.Input;

namespace KeyPulse.Infrastructure.Aggregation;

internal sealed class MouseGestureTracker
{
    private readonly int _thresholdX;
    private readonly int _thresholdY;
    private readonly Dictionary<MouseButton, Candidate> _candidates = [];

    public MouseGestureTracker()
        : this(
            Math.Max(1, RawInputNativeMethods.GetSystemMetrics(RawInputNativeMethods.SM_CXDRAG) / 2),
            Math.Max(1, RawInputNativeMethods.GetSystemMetrics(RawInputNativeMethods.SM_CYDRAG) / 2))
    {
    }

    internal MouseGestureTracker(int thresholdX, int thresholdY)
    {
        _thresholdX = Math.Max(1, thresholdX);
        _thresholdY = Math.Max(1, thresholdY);
    }

    public MouseButtonEvent? Process(MouseButtonEvent input)
    {
        if (input.Action == MouseButtonAction.Click)
        {
            return input;
        }

        if (input.Action == MouseButtonAction.Down)
        {
            if (input.Position is { } position)
            {
                _candidates[input.Button] = new Candidate(position);
            }
            else
            {
                _candidates.Remove(input.Button);
            }

            return null;
        }

        if (!_candidates.Remove(input.Button, out var candidate) || input.Position is not { } release)
        {
            return null;
        }

        candidate.Observe(release);
        if (candidate.MaximumX > _thresholdX || candidate.MaximumY > _thresholdY)
        {
            return null;
        }

        return input with { Action = MouseButtonAction.Click, Position = release };
    }

    public void Observe(PointerPosition? position)
    {
        if (position is not { } current)
        {
            return;
        }

        foreach (var candidate in _candidates.Values)
        {
            candidate.Observe(current);
        }
    }

    public void Reset() => _candidates.Clear();

    private sealed class Candidate(PointerPosition start)
    {
        public int MaximumX { get; private set; }

        public int MaximumY { get; private set; }

        public void Observe(PointerPosition current)
        {
            if (!string.Equals(start.Layout.Signature, current.Layout.Signature, StringComparison.Ordinal))
            {
                MaximumX = int.MaxValue;
                MaximumY = int.MaxValue;
                return;
            }

            MaximumX = Math.Max(MaximumX, Math.Abs(current.X - start.X));
            MaximumY = Math.Max(MaximumY, Math.Abs(current.Y - start.Y));
        }
    }
}
