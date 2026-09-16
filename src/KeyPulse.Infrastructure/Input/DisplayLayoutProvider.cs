using System.Security.Cryptography;
using System.Text;
using KeyPulse.Core.Models;

namespace KeyPulse.Infrastructure.Input;

public sealed class DisplayLayoutProvider : IDisposable
{
    private readonly Timer _refreshTimer;
    private DisplayLayout _current;

    public DisplayLayoutProvider()
    {
        _current = ReadLayout();
        _refreshTimer = new Timer(_ => Refresh(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public PointerPosition? GetCursorPosition()
    {
        if (!RawInputNativeMethods.GetCursorPos(out var point)) return null;
        var layout = Volatile.Read(ref _current);
        var monitor = layout.Monitors.FirstOrDefault(item =>
            point.X >= item.Left && point.X < item.Left + item.Width &&
            point.Y >= item.Top && point.Y < item.Top + item.Height);
        if (monitor is null) return null;
        return new PointerPosition(point.X, point.Y, layout, monitor);
    }

    public void Dispose() => _refreshTimer.Dispose();

    private void Refresh()
    {
        try
        {
            var next = ReadLayout();
            if (!string.Equals(next.Signature, _current.Signature, StringComparison.Ordinal))
            {
                Volatile.Write(ref _current, next);
            }
        }
        catch
        {
            // Retain the last valid layout; input collection must remain available.
        }
    }

    private static DisplayLayout ReadLayout()
    {
        var monitors = new List<DisplayMonitor>();
        RawInputNativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, AddMonitor, IntPtr.Zero);

        bool AddMonitor(IntPtr handle, IntPtr hdc, ref RECT monitorRect, IntPtr data)
        {
            var info = new MONITORINFOEX { cbSize = (uint)global::System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
            if (!RawInputNativeMethods.GetMonitorInfo(handle, ref info)) return true;
            var dpiX = 96u;
            var dpiY = 96u;
            try
            {
                if (RawInputNativeMethods.GetDpiForMonitor(handle, 0, out var readX, out var readY) == 0)
                {
                    dpiX = readX;
                    dpiY = readY;
                }
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }

            var rect = info.rcMonitor;
            monitors.Add(new DisplayMonitor(
                Hash(info.szDevice)[..16], rect.Left, rect.Top,
                rect.Right - rect.Left, rect.Bottom - rect.Top,
                dpiX, dpiY, (info.dwFlags & RawInputNativeMethods.MONITORINFOF_PRIMARY) != 0));
            return true;
        }

        if (monitors.Count == 0)
        {
            monitors.Add(new DisplayMonitor("primary", 0, 0, 1, 1, 96, 96, true));
        }

        monitors.Sort((left, right) =>
        {
            var byLeft = left.Left.CompareTo(right.Left);
            return byLeft != 0 ? byLeft : left.Top.CompareTo(right.Top);
        });
        var virtualLeft = RawInputNativeMethods.GetSystemMetrics(RawInputNativeMethods.SM_XVIRTUALSCREEN);
        var virtualTop = RawInputNativeMethods.GetSystemMetrics(RawInputNativeMethods.SM_YVIRTUALSCREEN);
        var virtualWidth = Math.Max(1, RawInputNativeMethods.GetSystemMetrics(RawInputNativeMethods.SM_CXVIRTUALSCREEN));
        var virtualHeight = Math.Max(1, RawInputNativeMethods.GetSystemMetrics(RawInputNativeMethods.SM_CYVIRTUALSCREEN));
        var signatureSource = string.Join(';', monitors.Select(m =>
            $"{m.Id}:{m.Left},{m.Top},{m.Width},{m.Height}:{m.DpiX:F2},{m.DpiY:F2}:{m.IsPrimary}"));
        return new DisplayLayout(Hash(signatureSource), virtualLeft, virtualTop, virtualWidth, virtualHeight, monitors);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
