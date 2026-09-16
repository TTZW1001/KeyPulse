using KeyPulse.Core.Events;
using Xunit;
using KeyPulse.Core.Models;
using KeyPulse.Infrastructure.Input;

namespace KeyPulse.Tests;

public class RawKeyboardParserTests
{
    private readonly RawKeyboardParser _parser = new();

    [Fact]
    public void CountsKeyDown()
    {
        var parsed = _parser.TryParse(0x41, 0, 0x1E, DateTimeOffset.UnixEpoch);
        Assert.NotNull(parsed);
        Assert.Equal("A", parsed!.Key.Name);
    }

    [Fact]
    public void EmitsKeyUp_ForShortcutStateTracking()
    {
        var parsed = _parser.TryParse(0x41, RawKeyboardParser.KeyBreak, 0x1E, DateTimeOffset.UnixEpoch);
        Assert.NotNull(parsed);
        Assert.False(parsed!.IsKeyDown);
        Assert.Equal("A", parsed.Key.Name);
    }

    [Fact]
    public void RecoversMaskedHotkey_FromValidScanCode()
    {
        var parsed = _parser.TryParse(0xFF, 0, 0x10, DateTimeOffset.UnixEpoch);

        Assert.NotNull(parsed);
        Assert.Equal("Q", parsed!.Key.Name);
        Assert.True(parsed.IsKeyDown);
    }

    [Theory]
    [InlineData(0x41, 0xFF)]
    [InlineData(0xFF, 0xFF)]
    [InlineData(0xFF, 0x00)]
    [InlineData(0x0100, 0x10)]
    public void IgnoresInvalidOrOverrunPackets(ushort virtualKey, ushort makeCode)
    {
        Assert.Null(_parser.TryParse(virtualKey, 0, makeCode, DateTimeOffset.UnixEpoch));
    }
}

public class RawMouseParserTests
{
    private readonly RawMouseParser _parser = new();

    [Fact]
    public void CountsButtonDown_IgnoresUp()
    {
        var down = new List<InputEvent>();
        _parser.Parse(0, RawMouseParser.LeftDown, 0, 0, 0, DateTimeOffset.UnixEpoch, down);
        Assert.Single(down);
        Assert.Equal(MouseButton.Left, Assert.IsType<MouseButtonEvent>(down[0]).Button);

        var up = new List<InputEvent>();
        _parser.Parse(0, 0x0002, 0, 0, 0, DateTimeOffset.UnixEpoch, up);
        Assert.Empty(up);
    }

    [Fact]
    public void WheelPositive_IsUp()
    {
        var output = new List<InputEvent>();
        _parser.Parse(0, RawMouseParser.Wheel, 120, 0, 0, DateTimeOffset.UnixEpoch, output);
        var wheel = Assert.IsType<MouseWheelEvent>(Assert.Single(output));
        Assert.False(wheel.Horizontal);
        Assert.True(wheel.Delta > 0);
    }

    [Fact]
    public void WheelNegative_IsDown()
    {
        var output = new List<InputEvent>();
        _parser.Parse(0, RawMouseParser.Wheel, -120, 0, 0, DateTimeOffset.UnixEpoch, output);
        var wheel = Assert.IsType<MouseWheelEvent>(Assert.Single(output));
        Assert.False(wheel.Horizontal);
        Assert.True(wheel.Delta < 0);
    }

    [Fact]
    public void RelativeMove_EmitsDelta_AbsoluteIgnored()
    {
        var relative = new List<InputEvent>();
        _parser.Parse(0, 0, 0, 3, -4, DateTimeOffset.UnixEpoch, relative);
        var move = Assert.IsType<MouseMoveEvent>(Assert.Single(relative));
        Assert.Equal(3, move.DeltaX);
        Assert.Equal(-4, move.DeltaY);

        var absolute = new List<InputEvent>();
        _parser.Parse(RawMouseParser.MoveAbsolute, 0, 0, 10, 10, DateTimeOffset.UnixEpoch, absolute);
        Assert.Empty(absolute);
    }
}
