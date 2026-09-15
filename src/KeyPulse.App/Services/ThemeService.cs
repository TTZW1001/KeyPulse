using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using Microsoft.Win32;

namespace KeyPulse.App.Services;

public sealed class ThemeService : IDisposable
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    private static readonly IReadOnlyDictionary<string, string> Light = new Dictionary<string, string>
    {
        ["AppBackground"] = "#F7F7F5",
        ["ContentBackground"] = "#FFFFFF",
        ["Divider"] = "#E4E4E1",
        ["PrimaryText"] = "#202020",
        ["SecondaryText"] = "#707070",
        ["Accent"] = "#4E6E9E",
        ["NavHover"] = "#EFEFEA",
        ["NavSelected"] = "#E4EAF1",
        ["PauseBanner"] = "#F3F0E8",
        ["ControlBackground"] = "#FFFFFF",
        ["ControlBorder"] = "#E4E4E1",
        ["GridHeader"] = "#F7F7F5",
        ["GridRowHover"] = "#F3F3F0",
        ["GridSelection"] = "#E4EAF1",
        ["Danger"] = "#A33B3B"
    };

    private static readonly IReadOnlyDictionary<string, string> Dark = new Dictionary<string, string>
    {
        ["AppBackground"] = "#181818",
        ["ContentBackground"] = "#202020",
        ["Divider"] = "#343434",
        ["PrimaryText"] = "#F2F2F2",
        ["SecondaryText"] = "#A8A8A8",
        ["Accent"] = "#4E6E9E",
        ["NavHover"] = "#242424",
        ["NavSelected"] = "#2A3340",
        ["PauseBanner"] = "#2A281F",
        ["ControlBackground"] = "#202020",
        ["ControlBorder"] = "#343434",
        ["GridHeader"] = "#1C1C1C",
        ["GridRowHover"] = "#262626",
        ["GridSelection"] = "#2A3340",
        ["Danger"] = "#D97A7A"
    };

    private readonly IUserSettings _settings;
    private bool _disposed;

    public ThemeService(IUserSettings settings)
    {
        _settings = settings;
    }

    public event Action? Changed;

    public ThemeMode Mode => _settings.Theme;

    public bool IsDarkEffective { get; private set; }

    public void Initialize()
    {
        Apply();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void SetMode(ThemeMode mode)
    {
        if (_settings.Theme == mode)
        {
            Apply();
            return;
        }

        _settings.Theme = mode;
        _settings.Save();
        Apply();
    }

    public void ApplyCaption(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var useDark = IsDarkEffective ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref useDark, sizeof(int));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void Apply()
    {
        IsDarkEffective = ResolveDark();
        var palette = IsDarkEffective ? Dark : Light;
        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        foreach (var pair in palette)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(pair.Value)!;
            app.Resources[pair.Key + "Color"] = color;
            SetBrush(app.Resources, pair.Key + "Brush", color);
            foreach (var dict in app.Resources.MergedDictionaries)
            {
                if (dict.Contains(pair.Key + "Brush"))
                {
                    SetBrush(dict, pair.Key + "Brush", color);
                }
            }
        }

        Changed?.Invoke();
    }

    private static void SetBrush(ResourceDictionary resources, string key, System.Windows.Media.Color color)
    {
        if (resources[key] is SolidColorBrush existing && !existing.IsFrozen)
        {
            existing.Color = color;
            return;
        }

        resources[key] = new SolidColorBrush(color);
    }

    private bool ResolveDark() =>
        _settings.Theme switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => !IsSystemLight()
        };

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General &&
            e.Category != UserPreferenceCategory.Color)
        {
            return;
        }

        if (_settings.Theme != ThemeMode.System)
        {
            return;
        }

        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        app.Dispatcher.Invoke(Apply);
    }

    private static bool IsSystemLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int i)
            {
                return i != 0;
            }
        }
        catch
        {
            // fall back to light
        }

        return true;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
