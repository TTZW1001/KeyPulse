using CommunityToolkit.Mvvm.ComponentModel;
using KeyPulse.App.Services;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;

namespace KeyPulse.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly ThemeService _theme;

    public SettingsViewModel(ThemeService theme, IAppPaths paths)
    {
        _theme = theme;
        DataPath = paths.DataDirectory;
        _theme.Changed += OnThemeChanged;
    }

    public string DataPath { get; }

    public bool IsLightTheme
    {
        get => _theme.Mode == ThemeMode.Light;
        set
        {
            if (value)
            {
                _theme.SetMode(ThemeMode.Light);
            }
        }
    }

    public bool IsDarkTheme
    {
        get => _theme.Mode == ThemeMode.Dark;
        set
        {
            if (value)
            {
                _theme.SetMode(ThemeMode.Dark);
            }
        }
    }

    public bool IsSystemTheme
    {
        get => _theme.Mode == ThemeMode.System;
        set
        {
            if (value)
            {
                _theme.SetMode(ThemeMode.System);
            }
        }
    }

    private void OnThemeChanged()
    {
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsSystemTheme));
    }
}
