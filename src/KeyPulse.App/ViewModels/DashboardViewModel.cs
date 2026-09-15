using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;

namespace KeyPulse.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IStatisticsReader _reader;

    public DashboardViewModel(IStatisticsReader reader)
    {
        _reader = reader;
        Refresh();
    }

    [ObservableProperty]
    private string _keyCountText = "0";

    [ObservableProperty]
    private string _clickCountText = "0";

    [ObservableProperty]
    private string _wheelCountText = "0";

    [ObservableProperty]
    private string _distanceText = "0 px";

    [ObservableProperty]
    private string _statusText = "正在统计";

    public void Refresh()
    {
        var snap = _reader.CaptureSnapshot();
        var culture = CultureInfo.CurrentCulture;
        var mouse = snap.Mouse;
        var keys = snap.KeyCounts.Values.Sum();
        var clicks = mouse.Left + mouse.Right + mouse.Middle + mouse.XButton1 + mouse.XButton2;
        var wheel = mouse.WheelUp + mouse.WheelDown + mouse.WheelLeft + mouse.WheelRight;

        KeyCountText = keys.ToString("N0", culture);
        ClickCountText = clicks.ToString("N0", culture);
        WheelCountText = wheel.ToString("N0", culture);
        DistanceText = FormatDistance(mouse.DistancePixels);
        StatusText = snap.State switch
        {
            TrackingState.Paused => "已暂停",
            TrackingState.Error => "监听失败",
            _ => "正在统计"
        };
    }

    private static string FormatDistance(double pixels)
    {
        var culture = CultureInfo.CurrentCulture;
        var meters = pixels / 96.0 * 0.0254;
        if (meters >= 100)
        {
            return (meters / 1000.0).ToString("0.00", culture) + " km";
        }

        if (meters >= 1)
        {
            return meters.ToString("0.0", culture) + " m";
        }

        return pixels.ToString("N0", culture) + " px";
    }
}
