using System.Text.Json;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;

namespace KeyPulse.Infrastructure.System;

public sealed class ExcludedAppList : IExcludedAppList
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _path;
    private readonly object _gate = new();
    private readonly HashSet<string> _user = new(StringComparer.OrdinalIgnoreCase);

    public ExcludedAppList(IAppPaths paths)
    {
        _path = paths.ExcludedAppsPath;
        Load();
    }

    public event Action? Changed;

    public bool IsExcluded(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        if (DefaultExcludedApps.Contains(processName))
        {
            return true;
        }

        lock (_gate)
        {
            return _user.Contains(processName);
        }
    }

    public IReadOnlyList<string> Snapshot()
    {
        var list = new List<string>(DefaultExcludedApps.Names);
        lock (_gate)
        {
            foreach (var name in _user)
            {
                if (!DefaultExcludedApps.Contains(name))
                {
                    list.Add(name);
                }
            }
        }

        return list;
    }

    public void Exclude(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return;
        }

        var name = Path.GetFileName(processName.Trim());
        if (string.IsNullOrWhiteSpace(name) || DefaultExcludedApps.Contains(name))
        {
            return;
        }

        lock (_gate)
        {
            if (!_user.Add(name))
            {
                return;
            }

            SaveUnlocked();
        }

        Changed?.Invoke();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return;
            }

            var json = File.ReadAllText(_path);
            var file = JsonSerializer.Deserialize<ExcludedFile>(json, JsonOptions);
            if (file?.Processes is null)
            {
                return;
            }

            lock (_gate)
            {
                foreach (var process in file.Processes)
                {
                    if (string.IsNullOrWhiteSpace(process))
                    {
                        continue;
                    }

                    var name = Path.GetFileName(process.Trim());
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        _user.Add(name);
                    }
                }
            }
        }
        catch
        {
            // fall back to defaults
        }
    }

    private void SaveUnlocked()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var file = new ExcludedFile
        {
            Processes = _user.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList()
        };
        File.WriteAllText(_path, JsonSerializer.Serialize(file, JsonOptions));
    }

    private sealed class ExcludedFile
    {
        public List<string> Processes { get; set; } = [];
    }
}
