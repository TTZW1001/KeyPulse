using CommunityToolkit.Mvvm.ComponentModel;
using Media = System.Windows.Media;

namespace KeyPulse.App.ViewModels;

public sealed partial class HeatmapKeyItem : ObservableObject
{
    public HeatmapKeyItem(
        string keyCode,
        string label,
        double left,
        double top,
        double width,
        double height)
    {
        KeyCode = keyCode;
        Label = label;
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    public string KeyCode { get; }

    public string Label { get; }

    public double Left { get; }

    public double Top { get; }

    public double Width { get; }

    public double Height { get; }

    [ObservableProperty]
    private long _count;

    [ObservableProperty]
    private string _hoverText = string.Empty;

    [ObservableProperty]
    private Media.Brush _fill = Media.Brushes.Transparent;

    [ObservableProperty]
    private Media.Brush _labelBrush = Media.Brushes.Black;
}
