using KeyPulse.Infrastructure.Input;
using Xunit;

namespace KeyPulse.Tests;

public class KeyMapperTests
{
    [Theory]
    [InlineData(0x41, 0, 0x1E, "A")]
    [InlineData(0x20, 0, 0x39, "Space")]
    [InlineData(0x0D, 0, 0x1C, "Enter")]
    [InlineData(0x1B, 0, 0x01, "Escape")]
    [InlineData(0x08, 0, 0x0E, "Backspace")]
    [InlineData(0x09, 0, 0x0F, "Tab")]
    [InlineData(0x11, 0, 0x1D, "LeftCtrl")]
    [InlineData(0x11, 0x02, 0x1D, "RightCtrl")]
    [InlineData(0x10, 0, 0x2A, "LeftShift")]
    [InlineData(0x10, 0, 0x36, "RightShift")]
    [InlineData(0x12, 0, 0x38, "LeftAlt")]
    [InlineData(0x12, 0x02, 0x38, "RightAlt")]
    [InlineData(0x5B, 0, 0x5B, "LeftWin")]
    [InlineData(0x26, 0, 0x48, "ArrowUp")]
    [InlineData(0x70, 0, 0x3B, "F1")]
    public void Map_CommonVirtualKeys(ushort virtualKey, ushort flags, ushort makeCode, string expected)
    {
        var actual = KeyMapper.Map(virtualKey, flags, makeCode);
        Assert.Equal(expected, actual.Name);
    }

    [Fact]
    public void Map_VKeyZero_UsesScanCode_NotVk00()
    {
        var actual = KeyMapper.Map(0, 0, 0x1E);
        Assert.Equal("A", actual.Name);
        Assert.NotEqual("VK_00", actual.Name);
    }

    [Fact]
    public void Map_ProcessKey_UsesScanCode()
    {
        var actual = KeyMapper.Map(KeyMapper.VirtualKeyProcessKey, 0, 0x1E);
        Assert.Equal("A", actual.Name);
    }

    [Fact]
    public void Map_UnknownScanCode_IsUnknown_NotVk00()
    {
        var actual = KeyMapper.Map(0, 0, 0xFE);
        Assert.Equal("Unknown", actual.Name);
        Assert.NotEqual("VK_00", actual.Name);
    }

    [Fact]
    public void MapScanCode_Space()
    {
        Assert.Equal("Space", KeyMapper.MapScanCode(0x39, 0).Name);
    }
}
