using System.Runtime.InteropServices;
using KeyPulse.Core.Events;
using KeyPulse.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace KeyPulse.Infrastructure.Input;

public sealed class RawInputService : IInputCapture, IDisposable
{
    private readonly ILogger<RawInputService> _logger;
    private readonly RawKeyboardParser _keyboardParser;
    private readonly RawMouseParser _mouseParser;
    private readonly DisplayLayoutProvider _displayLayout;
    private readonly uint _headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
    private readonly List<InputEvent> _mouseBuffer = new(8);
    private readonly object _gate = new();

    private HiddenInputWindow? _window;
    private Thread? _thread;
    private IntPtr _buffer = IntPtr.Zero;
    private uint _bufferSize;
    private bool _disposed;
    private volatile bool _isListening;
    private volatile string? _error;

    public RawInputService(
        ILogger<RawInputService> logger,
        RawKeyboardParser keyboardParser,
        RawMouseParser mouseParser,
        DisplayLayoutProvider displayLayout)
    {
        _logger = logger;
        _keyboardParser = keyboardParser;
        _mouseParser = mouseParser;
        _displayLayout = displayLayout;
    }

    public event EventHandler<InputEvent>? InputReceived;

    public bool IsListening => _isListening;

    public string? Error => _error;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (IsListening)
            {
                return Task.CompletedTask;
            }

            if (_thread is not null || _window is not null)
            {
                StopCore();
            }

            _error = null;
            using var ready = new ManualResetEventSlim(false);
            Exception? startError = null;

            _thread = new Thread(() =>
            {
                HiddenInputWindow? window = null;
                try
                {
                    window = new HiddenInputWindow();
                    window.RawInputReceived += OnRawInput;
                    window.Create();
                    _window = window;

                    if (!RegisterDevices(window.Handle))
                    {
                        startError = new InvalidOperationException(
                            "RegisterRawInputDevices failed. Win32=" + Marshal.GetLastWin32Error());
                        _error = startError.Message;
                        window.Dispose();
                        _window = null;
                        return;
                    }

                    _isListening = true;
                }
                catch (Exception ex)
                {
                    startError = ex;
                    _error = ex.Message;
                    window?.Dispose();
                    _window = null;
                }
                finally
                {
                    ready.Set();
                }

                if (Error is null && window is not null)
                {
                    try
                    {
                        window.RunMessageLoop();
                    }
                    finally
                    {
                        _isListening = false;
                        window.RawInputReceived -= OnRawInput;
                        window.Dispose();
                        if (ReferenceEquals(_window, window))
                        {
                            _window = null;
                        }
                    }
                }
            })
            {
                IsBackground = true,
                Name = "KeyPulse.RawInput"
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();

            if (!ready.Wait(TimeSpan.FromSeconds(5), cancellationToken))
            {
                _error = "Raw Input thread did not start in time.";
                _logger.LogError("Raw Input listener failed to start: {Error}", Error);
                return Task.CompletedTask;
            }

            if (Error is not null)
            {
                _logger.LogError(startError, "Raw Input listener failed to start: {Error}", Error);
                return Task.CompletedTask;
            }

            _logger.LogInformation("Raw Input listener started");
            return Task.CompletedTask;
        }
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            StopCore();
            return Task.CompletedTask;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_gate)
        {
            StopCore();
        }
    }

    private void StopCore()
    {
        if (_thread is null && _window is null)
        {
            return;
        }

        var window = _window;
        if (window is not null && window.Handle != IntPtr.Zero)
        {
            UnregisterDevices();
            window.RequestClose();
        }

        if (_thread is not null && !_thread.Join(TimeSpan.FromSeconds(2)))
        {
            _logger.LogWarning("Raw Input thread did not exit within timeout");
        }

        _thread = null;
        _window?.Dispose();
        _window = null;
        _isListening = false;

        if (_buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_buffer);
            _buffer = IntPtr.Zero;
            _bufferSize = 0;
        }

        if (Error is null)
        {
            _logger.LogInformation("Raw Input listener stopped");
        }
    }

    private static bool RegisterDevices(IntPtr hwnd)
    {
        var devices = new[]
        {
            new RAWINPUTDEVICE
            {
                usUsagePage = RawInputNativeMethods.HID_USAGE_PAGE_GENERIC,
                usUsage = RawInputNativeMethods.HID_USAGE_GENERIC_KEYBOARD,
                dwFlags = RawInputNativeMethods.RIDEV_INPUTSINK,
                hwndTarget = hwnd
            },
            new RAWINPUTDEVICE
            {
                usUsagePage = RawInputNativeMethods.HID_USAGE_PAGE_GENERIC,
                usUsage = RawInputNativeMethods.HID_USAGE_GENERIC_MOUSE,
                dwFlags = RawInputNativeMethods.RIDEV_INPUTSINK,
                hwndTarget = hwnd
            }
        };

        return RawInputNativeMethods.RegisterRawInputDevices(
            devices,
            (uint)devices.Length,
            (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
    }

    private static void UnregisterDevices()
    {
        var devices = new[]
        {
            new RAWINPUTDEVICE
            {
                usUsagePage = RawInputNativeMethods.HID_USAGE_PAGE_GENERIC,
                usUsage = RawInputNativeMethods.HID_USAGE_GENERIC_KEYBOARD,
                dwFlags = RawInputNativeMethods.RIDEV_REMOVE,
                hwndTarget = IntPtr.Zero
            },
            new RAWINPUTDEVICE
            {
                usUsagePage = RawInputNativeMethods.HID_USAGE_PAGE_GENERIC,
                usUsage = RawInputNativeMethods.HID_USAGE_GENERIC_MOUSE,
                dwFlags = RawInputNativeMethods.RIDEV_REMOVE,
                hwndTarget = IntPtr.Zero
            }
        };

        RawInputNativeMethods.RegisterRawInputDevices(
            devices,
            (uint)devices.Length,
            (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
    }

    private void OnRawInput(IntPtr lParam)
    {
        uint size = 0;
        RawInputNativeMethods.GetRawInputData(
            lParam,
            RawInputNativeMethods.RID_INPUT,
            IntPtr.Zero,
            ref size,
            _headerSize);

        if (size == 0)
        {
            return;
        }

        EnsureBuffer(size);
        var bufferSize = _bufferSize;
        var written = RawInputNativeMethods.GetRawInputData(
            lParam,
            RawInputNativeMethods.RID_INPUT,
            _buffer,
            ref bufferSize,
            _headerSize);

        if (written == 0 || written == 0xFFFFFFFF)
        {
            return;
        }

        var header = Marshal.PtrToStructure<RAWINPUTHEADER>(_buffer);
        var timestamp = DateTimeOffset.Now;

        if (header.dwType == RawInputNativeMethods.RIM_TYPEKEYBOARD)
        {
            var keyboard = Marshal.PtrToStructure<RAWKEYBOARD>(IntPtr.Add(_buffer, (int)_headerSize));
            var parsed = _keyboardParser.TryParse(keyboard.VKey, keyboard.Flags, keyboard.MakeCode, timestamp);
            if (parsed is not null)
            {
                InputReceived?.Invoke(this, parsed);
            }
        }
        else if (header.dwType == RawInputNativeMethods.RIM_TYPEMOUSE)
        {
            var mouse = Marshal.PtrToStructure<RAWMOUSE>(IntPtr.Add(_buffer, (int)_headerSize));
            _mouseBuffer.Clear();
            _mouseParser.Parse(
                mouse.usFlags,
                mouse.usButtonFlags,
                mouse.usButtonData,
                mouse.lLastX,
                mouse.lLastY,
                timestamp,
                _mouseBuffer);

            var position = _mouseBuffer.Any(item => item is MouseMoveEvent or MouseButtonEvent)
                ? _displayLayout.GetCursorPosition()
                : null;
            foreach (var parsed in _mouseBuffer)
            {
                var enriched = parsed switch
                {
                    MouseMoveEvent move => move with { Position = position },
                    MouseButtonEvent button => button with { Position = position },
                    _ => parsed
                };
                InputReceived?.Invoke(this, enriched);
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
}
