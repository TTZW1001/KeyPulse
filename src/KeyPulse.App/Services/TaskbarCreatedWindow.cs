using System.Runtime.InteropServices;
using System.Windows.Forms;
using KeyPulse.Infrastructure.System;

namespace KeyPulse.App.Services;

internal sealed class TaskbarCreatedWindow : NativeWindow, IDisposable
{
    public const string MessageName = "TaskbarCreated";
    private const uint MsgfltAllow = 1;

    private readonly TaskbarCreatedRouter _router;
    private bool _disposed;

    public TaskbarCreatedWindow(TaskbarCreatedRouter router)
    {
        _router = router;
        CreateHandle(new CreateParams());
        if (Handle != IntPtr.Zero && router is not null)
        {
            ChangeWindowMessageFilterEx(Handle, NativeMessageId, MsgfltAllow, IntPtr.Zero);
        }
    }

    public static uint NativeMessageId { get; } = RegisterWindowMessage(MessageName);

    protected override void WndProc(ref Message m)
    {
        _router.TryHandle(m.Msg);
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DestroyHandle();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool ChangeWindowMessageFilterEx(
        IntPtr hwnd,
        uint message,
        uint action,
        IntPtr changeFilterStruct);
}
