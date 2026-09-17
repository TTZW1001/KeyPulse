using System.Text.Json;
using System.Text.Json.Serialization;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;

namespace KeyPulse.Infrastructure.System;

public sealed class JsonUserSettings : IUserSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;
    private readonly SettingsFile _file;

    public JsonUserSettings(IAppPaths paths)
    {
        _path = paths.SettingsPath;
        _file = Load(_path, File.Exists(_path));
    }

    public bool HideToTrayHintDismissed
    {
        get => _file.HideToTrayHintDismissed;
        set => _file.HideToTrayHintDismissed = value;
    }

    public ThemeMode Theme
    {
        get => _file.Theme;
        set => _file.Theme = value;
    }

    public KeyboardLayoutKind KeyboardLayout
    {
        get => _file.KeyboardLayout;
        set => _file.KeyboardLayout = value;
    }

    public bool KeyboardLayoutExplicitlyChosen
    {
        get => _file.KeyboardLayoutExplicitlyChosen;
        set => _file.KeyboardLayoutExplicitlyChosen = value;
    }

    public bool ShortcutStatsEnabled
    {
        get => _file.ShortcutStatsEnabled;
        set => _file.ShortcutStatsEnabled = value;
    }

    public bool ScreenPositionStatsEnabled
    {
        get => _file.ScreenPositionStatsEnabled;
        set => _file.ScreenPositionStatsEnabled = value;
    }

    public int PositionRetentionDays
    {
        get => _file.PositionRetentionDays ?? 0;
        set => _file.PositionRetentionDays = value;
    }

    public bool ShowInsights
    {
        get => _file.ShowInsights;
        set => _file.ShowInsights = value;
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(_file, JsonOptions));
    }

    private static SettingsFile Load(string path, bool existingInstallation)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<SettingsFile>(json, JsonOptions) ?? new SettingsFile();
            }
        }
        catch
        {
            // fall back to defaults
        }

        return new SettingsFile { PositionRetentionDays = existingInstallation ? 0 : 90 };
    }

    private sealed class SettingsFile
    {
        public bool HideToTrayHintDismissed { get; set; }

        public ThemeMode Theme { get; set; } = ThemeMode.System;

        public KeyboardLayoutKind KeyboardLayout { get; set; } = KeyboardLayoutKind.CompactLaptop;

        public bool KeyboardLayoutExplicitlyChosen { get; set; }

        public bool ShortcutStatsEnabled { get; set; } = true;

        public bool ScreenPositionStatsEnabled { get; set; }

        public int? PositionRetentionDays { get; set; }

        public bool ShowInsights { get; set; } = true;
    }
}
