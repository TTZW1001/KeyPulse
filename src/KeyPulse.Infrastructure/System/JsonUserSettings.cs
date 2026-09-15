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
        _file = Load(_path);
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

    public void Save()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(_file, JsonOptions));
    }

    private static SettingsFile Load(string path)
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

        return new SettingsFile();
    }

    private sealed class SettingsFile
    {
        public bool HideToTrayHintDismissed { get; set; }

        public ThemeMode Theme { get; set; } = ThemeMode.System;
    }
}
