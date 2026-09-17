using KeyPulse.Core;
using KeyPulse.Core.Events;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Models;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Aggregation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KeyPulse.Tests;

public sealed class MouseGestureTests
{
    [Fact]
    public void Click_IsCountedOnRelease_AtReleasePosition()
    {
        using var aggregator = Create();
        var start = Position(100, 100);
        var release = Position(101, 101);

        aggregator.Record(Button(MouseButtonAction.Down, start));
        Assert.Equal(0, aggregator.CaptureSnapshot().Mouse.Left);

        aggregator.Record(Button(MouseButtonAction.Up, release));

        Assert.Equal(1, aggregator.CaptureSnapshot().Mouse.Left);
        Assert.Equal(101, aggregator.CaptureUnflushed().Pointer!.Clicks.Single().Key.X);
        Assert.Equal(101, aggregator.CaptureUnflushed().Pointer!.Clicks.Single().Key.Y);
    }

    [Fact]
    public void DragBeyondSystemThreshold_DoesNotCountAsClick()
    {
        using var aggregator = Create();
        aggregator.Record(Button(MouseButtonAction.Down, Position(100, 100)));
        aggregator.Record(Move(Position(120, 100)));
        aggregator.Record(Button(MouseButtonAction.Up, Position(100, 100)));

        Assert.Equal(0, aggregator.CaptureSnapshot().Mouse.Left);
        Assert.Empty(aggregator.CaptureUnflushed().Pointer!.Clicks);
    }

    [Fact]
    public void MissingPair_DoesNotCreateClick_AndPauseResetsCandidate()
    {
        using var aggregator = Create();
        aggregator.Record(Button(MouseButtonAction.Up, Position(100, 100)));
        aggregator.Record(Button(MouseButtonAction.Down, Position(100, 100)));
        aggregator.SetState(TrackingState.Paused);
        aggregator.SetState(TrackingState.Running);
        aggregator.Record(Button(MouseButtonAction.Up, Position(100, 100)));

        Assert.Equal(0, aggregator.CaptureSnapshot().Mouse.Left);
    }

    [Fact]
    public void TwoCompletedClicks_CountTwice()
    {
        using var aggregator = Create();
        for (var i = 0; i < 2; i++)
        {
            aggregator.Record(Button(MouseButtonAction.Down, Position(100, 100)));
            aggregator.Record(Button(MouseButtonAction.Up, Position(100, 100)));
        }

        Assert.Equal(2, aggregator.CaptureSnapshot().Mouse.Left);
    }

    private static InputAggregator Create() =>
        new(new SilentCapture(), NullLogger<InputAggregator>.Instance, settings: new FakeSettings());

    private static MouseButtonEvent Button(MouseButtonAction action, PointerPosition position) => new()
    {
        Timestamp = DateTimeOffset.Now,
        Button = MouseButton.Left,
        Action = action,
        Position = position
    };

    private static MouseMoveEvent Move(PointerPosition position) => new()
    {
        Timestamp = DateTimeOffset.Now,
        DeltaX = 0,
        DeltaY = 0,
        Position = position
    };

    private static PointerPosition Position(int x, int y)
    {
        var monitor = new DisplayMonitor("display-1", 0, 0, 1920, 1080, 96, 96, true);
        var layout = new DisplayLayout("layout-1", 0, 0, 1920, 1080, [monitor]);
        return new PointerPosition(x, y, layout, monitor);
    }

    private sealed class SilentCapture : IInputCapture
    {
        public event EventHandler<InputEvent>? InputReceived { add { } remove { } }
        public bool IsListening => true;
        public string? Error => null;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
    }

    private sealed class FakeSettings : IUserSettings
    {
        public bool HideToTrayHintDismissed { get; set; }
        public ThemeMode Theme { get; set; }
        public KeyboardLayoutKind KeyboardLayout { get; set; }
        public bool KeyboardLayoutExplicitlyChosen { get; set; }
        public bool ShortcutStatsEnabled { get; set; } = true;
        public bool ScreenPositionStatsEnabled { get; set; } = true;
        public void Save() { }
    }
}
