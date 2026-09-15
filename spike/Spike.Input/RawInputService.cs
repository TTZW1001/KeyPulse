using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Spike.Input;

internal sealed class RawInputService : IDisposable
{
    private readonly InputCounters _counters;
    private readonly uint _headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
    private HwndSource? _source;
    private Thread? _thread;
    private IntPtr _buffer = IntPtr.Zero;
    private uint _bufferSize;
    private bool _disposed;

    public RawInputService(InputCounters counters)
    {
        _counters = counters;
    }

    public IntPtr Hwnd { get; private set; }
    public string? Error { get; private set; }
    public bool IsListening => Error is null && Hwnd != IntPtr.Zero;

    public void Start()
    {
        using var ready = new ManualResetEventSlim(false);
        _thread = new Thread(() =>
        {
            try
            {
                var parameters = new HwndSourceParameters("KeyPulse.Spike.RawInput")
                {
                    Width = 0,
                    Height = 0,
                    PositionX = -32000,
                    PositionY = -32000,
                    WindowStyle = NativeMethods.WS_POPUP,
                    ExtendedWindowStyle = NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW
                };

                _source = new HwndSource(parameters);
                _source.AddHook(Hook);
                Hwnd = _source.Handle;

                var devices = new[]
                {
                    new RAWINPUTDEVICE
                    {
                        usUsagePage = NativeMethods.HID_USAGE_PAGE_GENERIC,
                        usUsage = NativeMethods.HID_USAGE_GENERIC_KEYBOARD,
                        dwFlags = NativeMethods.RIDEV_INPUTSINK,
                        hwndTarget = Hwnd
                    },
                    new RAWINPUTDEVICE
                    {
                        usUsagePage = NativeMethods.HID_USAGE_PAGE_GENERIC,
                        usUsage = NativeMethods.HID_USAGE_GENERIC_MOUSE,
                        dwFlags = NativeMethods.RIDEV_INPUTSINK,
                        hwndTarget = Hwnd
                    }
                };

                if (!NativeMethods.RegisterRawInputDevices(
                        devices,
                        (uint)devices.Length,
                        (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
                {
                    Error = "RegisterRawInputDevices failed. Win32=" + Marshal.GetLastWin32Error();
                }
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
            finally
            {
                ready.Set();
            }

            if (Error is null)
            {
                Dispatcher.Run();
            }
        })
        {
            IsBackground = true,
            Name = "Spike.RawInput"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(5)))
        {
            Error = "Raw input thread did not start in time.";
        }
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_INPUT)
        {
            ProcessInput(lParam);
        }

        return IntPtr.Zero;
    }

    private void ProcessInput(IntPtr lParam)
    {
        // parse → increment counter → return. No UI, no DB, no logging here.
        uint size = 0;
        NativeMethods.GetRawInputData(
            lParam,
            NativeMethods.RID_INPUT,
            IntPtr.Zero,
            ref size,
            _headerSize);

        if (size == 0)
        {
            return;
        }

        EnsureBuffer(size);
        var bufferSize = _bufferSize;
        var written = NativeMethods.GetRawInputData(
            lParam,
            NativeMethods.RID_INPUT,
            _buffer,
            ref bufferSize,
            _headerSize);

        if (written == 0 || written == 0xFFFFFFFF)
        {
            return;
        }

        _counters.IncrementWmInput();
        var header = Marshal.PtrToStructure<RAWINPUTHEADER>(_buffer);

        if (header.dwType == NativeMethods.RIM_TYPEKEYBOARD)
        {
            var keyboard = Marshal.PtrToStructure<RAWKEYBOARD>(IntPtr.Add(_buffer, (int)_headerSize));
            if ((keyboard.Flags & NativeMethods.RI_KEY_BREAK) != 0)
            {
                return;
            }

            var name = KeyMapper.Map(keyboard.VKey, keyboard.Flags, keyboard.MakeCode);
            _counters.AddKey(name);
        }
        else if (header.dwType == NativeMethods.RIM_TYPEMOUSE)
        {
            var mouse = Marshal.PtrToStructure<RAWMOUSE>(IntPtr.Add(_buffer, (int)_headerSize));
            var flags = mouse.usButtonFlags;

            if ((flags & NativeMethods.RI_MOUSE_LEFT_BUTTON_DOWN) != 0)
            {
                _counters.AddMouseButton("Left");
            }

            if ((flags & NativeMethods.RI_MOUSE_RIGHT_BUTTON_DOWN) != 0)
            {
                _counters.AddMouseButton("Right");
            }

            if ((flags & NativeMethods.RI_MOUSE_MIDDLE_BUTTON_DOWN) != 0)
            {
                _counters.AddMouseButton("Middle");
            }

            if ((flags & NativeMethods.RI_MOUSE_BUTTON_4_DOWN) != 0)
            {
                _counters.AddMouseButton("X1");
            }

            if ((flags & NativeMethods.RI_MOUSE_BUTTON_5_DOWN) != 0)
            {
                _counters.AddMouseButton("X2");
            }

            if ((flags & NativeMethods.RI_MOUSE_WHEEL) != 0)
            {
                _counters.AddWheel(mouse.usButtonData, 0);
            }

            if ((flags & NativeMethods.RI_MOUSE_HWHEEL) != 0)
            {
                _counters.AddWheel(0, mouse.usButtonData);
            }

            if ((mouse.usFlags & NativeMethods.MOUSE_MOVE_ABSOLUTE) == 0 &&
                (mouse.lLastX != 0 || mouse.lLastY != 0))
            {
                var dx = (double)mouse.lLastX;
                var dy = (double)mouse.lLastY;
                _counters.AddDistance(Math.Sqrt((dx * dx) + (dy * dy)));
            }
        }
    }

    private void EnsureBuffer(uint size)
    {
        if (_buffer != IntPtr.Zero && _bufferSize >= size)
        {
            return;
        }

        if (_buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_buffer);
        }

        _buffer = Marshal.AllocHGlobal((int)size);
        _bufferSize = size;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (Hwnd != IntPtr.Zero)
        {
            var devices = new[]
            {
                new RAWINPUTDEVICE
                {
                    usUsagePage = NativeMethods.HID_USAGE_PAGE_GENERIC,
                    usUsage = NativeMethods.HID_USAGE_GENERIC_KEYBOARD,
                    dwFlags = NativeMethods.RIDEV_REMOVE,
                    hwndTarget = IntPtr.Zero
                },
                new RAWINPUTDEVICE
                {
                    usUsagePage = NativeMethods.HID_USAGE_PAGE_GENERIC,
                    usUsage = NativeMethods.HID_USAGE_GENERIC_MOUSE,
                    dwFlags = NativeMethods.RIDEV_REMOVE,
                    hwndTarget = IntPtr.Zero
                }
            };
            NativeMethods.RegisterRawInputDevices(
                devices,
                (uint)devices.Length,
                (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
        }

        try
        {
            _source?.Dispatcher.InvokeShutdown();
        }
        catch
        {
            // already shutting down
        }

        if (_thread is not null && !_thread.Join(TimeSpan.FromSeconds(2)))
        {
            // background thread will exit with the process
        }

        _source?.RemoveHook(Hook);
        _source?.Dispose();
        _source = null;

        if (_buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_buffer);
            _buffer = IntPtr.Zero;
        }
    }
}
