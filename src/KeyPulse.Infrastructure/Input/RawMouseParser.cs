using KeyPulse.Core.Events;
using KeyPulse.Core.Models;

namespace KeyPulse.Infrastructure.Input;

public sealed class RawMouseParser
{
    public const ushort LeftDown = RawInputNativeMethods.RI_MOUSE_LEFT_BUTTON_DOWN;
    public const ushort LeftUp = RawInputNativeMethods.RI_MOUSE_LEFT_BUTTON_UP;
    public const ushort RightDown = RawInputNativeMethods.RI_MOUSE_RIGHT_BUTTON_DOWN;
    public const ushort RightUp = RawInputNativeMethods.RI_MOUSE_RIGHT_BUTTON_UP;
    public const ushort MiddleDown = RawInputNativeMethods.RI_MOUSE_MIDDLE_BUTTON_DOWN;
    public const ushort MiddleUp = RawInputNativeMethods.RI_MOUSE_MIDDLE_BUTTON_UP;
    public const ushort X1Down = RawInputNativeMethods.RI_MOUSE_BUTTON_4_DOWN;
    public const ushort X1Up = RawInputNativeMethods.RI_MOUSE_BUTTON_4_UP;
    public const ushort X2Down = RawInputNativeMethods.RI_MOUSE_BUTTON_5_DOWN;
    public const ushort X2Up = RawInputNativeMethods.RI_MOUSE_BUTTON_5_UP;
    public const ushort Wheel = RawInputNativeMethods.RI_MOUSE_WHEEL;
    public const ushort HorizontalWheel = RawInputNativeMethods.RI_MOUSE_HWHEEL;
    public const ushort MoveAbsolute = RawInputNativeMethods.MOUSE_MOVE_ABSOLUTE;

    public void Parse(
        ushort usFlags,
        ushort buttonFlags,
        short buttonData,
        int lastX,
        int lastY,
        DateTimeOffset timestamp,
        ICollection<InputEvent> output)
    {
        if ((buttonFlags & LeftDown) != 0)
        {
            output.Add(Button(MouseButton.Left, MouseButtonAction.Down, timestamp));
        }

        if ((buttonFlags & LeftUp) != 0)
        {
            output.Add(Button(MouseButton.Left, MouseButtonAction.Up, timestamp));
        }

        if ((buttonFlags & RightDown) != 0)
        {
            output.Add(Button(MouseButton.Right, MouseButtonAction.Down, timestamp));
        }

        if ((buttonFlags & RightUp) != 0)
        {
            output.Add(Button(MouseButton.Right, MouseButtonAction.Up, timestamp));
        }

        if ((buttonFlags & MiddleDown) != 0)
        {
            output.Add(Button(MouseButton.Middle, MouseButtonAction.Down, timestamp));
        }

        if ((buttonFlags & MiddleUp) != 0)
        {
            output.Add(Button(MouseButton.Middle, MouseButtonAction.Up, timestamp));
        }

        if ((buttonFlags & X1Down) != 0)
        {
            output.Add(Button(MouseButton.XButton1, MouseButtonAction.Down, timestamp));
        }

        if ((buttonFlags & X1Up) != 0)
        {
            output.Add(Button(MouseButton.XButton1, MouseButtonAction.Up, timestamp));
        }

        if ((buttonFlags & X2Down) != 0)
        {
            output.Add(Button(MouseButton.XButton2, MouseButtonAction.Down, timestamp));
        }

        if ((buttonFlags & X2Up) != 0)
        {
            output.Add(Button(MouseButton.XButton2, MouseButtonAction.Up, timestamp));
        }

        if ((buttonFlags & Wheel) != 0)
        {
            output.Add(new MouseWheelEvent
            {
                Timestamp = timestamp,
                Delta = buttonData,
                Horizontal = false
            });
        }

        if ((buttonFlags & HorizontalWheel) != 0)
        {
            output.Add(new MouseWheelEvent
            {
                Timestamp = timestamp,
                Delta = buttonData,
                Horizontal = true
            });
        }

        if ((usFlags & MoveAbsolute) == 0 && (lastX != 0 || lastY != 0))
        {
            output.Add(new MouseMoveEvent
            {
                Timestamp = timestamp,
                DeltaX = lastX,
                DeltaY = lastY
            });
        }
    }

    private static MouseButtonEvent Button(
        MouseButton button,
        MouseButtonAction action,
        DateTimeOffset timestamp) =>
        new()
        {
            Timestamp = timestamp,
            Button = button,
            Action = action
        };
}
