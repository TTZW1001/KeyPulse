using System.Diagnostics;
using System.Text;

namespace KeyPulse.Infrastructure.Input;

public sealed class WindowsForegroundProcessNative : IForegroundProcessNative
{
    public nint GetForegroundWindow() => ForegroundNativeMethods.GetForegroundWindow();

    public uint GetWindowThreadProcessId(nint hwnd, out uint processId) =>
        ForegroundNativeMethods.GetWindowThreadProcessId(hwnd, out processId);

    public string? QueryImagePath(uint processId)
    {
        if (processId == 0)
        {
            return null;
        }

        var handle = ForegroundNativeMethods.OpenProcess(
            ForegroundNativeMethods.ProcessQueryLimitedInformation,
            false,
            processId);
        if (handle == 0)
        {
            return null;
        }

        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            if (ForegroundNativeMethods.QueryFullProcessImageName(handle, 0, buffer, ref size) &&
                size > 0)
            {
                return buffer.ToString();
            }

            buffer.EnsureCapacity(32768);
            size = buffer.Capacity;
            if (ForegroundNativeMethods.QueryFullProcessImageName(handle, 0, buffer, ref size) &&
                size > 0)
            {
                return buffer.ToString();
            }

            return null;
        }
        finally
        {
            ForegroundNativeMethods.CloseHandle(handle);
        }
    }

    public string? QueryFileDescription(string imagePath)
    {
        try
        {
            var description = FileVersionInfo.GetVersionInfo(imagePath).FileDescription;
            return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        }
        catch
        {
            return null;
        }
    }
}
