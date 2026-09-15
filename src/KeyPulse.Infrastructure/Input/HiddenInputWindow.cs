using System.Runtime.InteropServices;

namespace KeyPulse.Infrastructure.Input;

internal sealed class HiddenInputWindow : IDisposable
{
    private readonly RawInputNativeMethods.WndProc _wndProc;
    private readonly string _className = "KeyPulse.RawInputWindow." + Guid.NewGuid().ToString("N");
    private readonly IntPtr _module;
    private bool _classRegistered;
    private bool _disposed;

    public HiddenInputWindow()
    {
        _wndProc = WndProc;
        _module = RawInputNativeMethods.GetModuleHandle(null);
    }

    public IntPtr Handle { get; private set; }

    public event Action<IntPtr>? RawInputReceived;

    public void Create()
    {
        var windowClass = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = _wndProc,
            hInstance = _module,
            lpszClassName = _className
        };

        if (RawInputNativeMethods.RegisterClassEx(ref windowClass) == 0)
        {
            throw new InvalidOperationException(
                "RegisterClassEx failed. Win32=" + Marshal.GetLastWin32Error());
        }

        _classRegistered = true;

        Handle = RawInputNativeMethods.CreateWindowEx(
            RawInputNativeMethods.WS_EX_NOACTIVATE | RawInputNativeMethods.WS_EX_TOOLWINDOW,
            _className,
            "KeyPulse.RawInput",
            RawInputNativeMethods.WS_POPUP,
            -32000,
            -32000,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            _module,
            IntPtr.Zero);

        if (Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "CreateWindowEx failed. Win32=" + Marshal.GetLastWin32Error());
        }
    }

    public void RunMessageLoop()
    {
        MSG message;
        while (RawInputNativeMethods.GetMessage(out message, IntPtr.Zero, 0, 0) > 0)
        {
            RawInputNativeMethods.TranslateMessage(ref message);
            RawInputNativeMethods.DispatchMessage(ref message);
        }
    }

    public void RequestClose()
    {
        if (Handle != IntPtr.Zero)
        {
            RawInputNativeMethods.PostMessage(
                Handle,
                RawInputNativeMethods.WM_CLOSE,
                IntPtr.Zero,
                IntPtr.Zero);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (Handle != IntPtr.Zero)
        {
            RawInputNativeMethods.DestroyWindow(Handle);
            Handle = IntPtr.Zero;
        }

        if (_classRegistered)
        {
            RawInputNativeMethods.UnregisterClass(_className, _module);
            _classRegistered = false;
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == RawInputNativeMethods.WM_INPUT)
        {
            RawInputReceived?.Invoke(lParam);
        }
        else if (msg == RawInputNativeMethods.WM_CLOSE)
        {
            RawInputNativeMethods.DestroyWindow(hWnd);
            return IntPtr.Zero;
        }
        else if (msg == RawInputNativeMethods.WM_DESTROY)
        {
            Handle = IntPtr.Zero;
            RawInputNativeMethods.PostQuitMessage(0);
            return IntPtr.Zero;
        }

        return RawInputNativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }
}
