using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyPulse.App.Services;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using KeyPulse.Infrastructure.Input;
using KeyPulse.Infrastructure.Persistence;
using System.Windows.Threading;
using WpfMessageBox = System.Windows.MessageBox;

namespace KeyPulse.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    public const string ClearConfirmText = ProductInfo.ClearConfirm;

    private readonly ThemeService _theme;
    private readonly IStartupService _startup;
    private readonly IExcludedAppList _exclusions;
    private readonly IFlushService _flush;
    private readonly IStatisticsExport _export;
    private readonly IUserSettings _settings;
    private readonly InputDiagnostics _inputDiagnostics;
    private readonly Dispatcher _dispatcher;
    private readonly object _gate = new();
    private bool _busy;

    public SettingsViewModel(
        ThemeService theme,
        IAppPaths paths,
        IStartupService startup,
        IExcludedAppList exclusions,
        IFlushService flush,
        IStatisticsExport export,
        IUserSettings settings,
        InputDiagnostics inputDiagnostics)
    {
        _theme = theme;
        _startup = startup;
        _exclusions = exclusions;
        _flush = flush;
        _export = export;
        _settings = settings;
        _inputDiagnostics = inputDiagnostics;
        _dispatcher = Dispatcher.CurrentDispatcher;
        DataPath = paths.DataDirectory;
        DataRoot = paths.RootDirectory;
        ProductName = ProductInfo.Name;
        VersionText = ProductInfo.Version;
        PrivacyNotice = ProductInfo.PrivacyNotice;
        _theme.Changed += OnThemeChanged;
        _exclusions.Changed += ReloadExcluded;
        _inputDiagnostics.Changed += OnInputDiagnosticsChanged;
        ReloadExcluded();
        ReloadInputDiagnostics();
    }

    public string DataPath { get; }

    public string DataRoot { get; }

    public string ProductName { get; }

    public string VersionText { get; }

    public string PrivacyNotice { get; }

    public ObservableCollection<ExcludedAppRow> ExcludedApps { get; } = [];

    public ObservableCollection<string> InputDiagnosticRows { get; } = [];

    [ObservableProperty]
    private string _newProcessName = string.Empty;

    [ObservableProperty]
    private string _dataStatus = string.Empty;

    public bool StartupEnabled
    {
        get => _startup.IsEnabled;
        set
        {
            if (value)
            {
                _startup.Enable();
            }
            else
            {
                _startup.Disable();
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(StartMinimized));
        }
    }

    public bool StartMinimized => true;

    public bool ShortcutStatsEnabled
    {
        get => _settings.ShortcutStatsEnabled;
        set
        {
            if (_settings.ShortcutStatsEnabled == value) return;
            _settings.ShortcutStatsEnabled = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public bool ScreenPositionStatsEnabled
    {
        get => _settings.ScreenPositionStatsEnabled;
        set
        {
            if (_settings.ScreenPositionStatsEnabled == value) return;
            _settings.ScreenPositionStatsEnabled = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public bool InputDiagnosticsEnabled
    {
        get => _inputDiagnostics.Enabled;
        set
        {
            if (_inputDiagnostics.Enabled == value) return;
            _inputDiagnostics.SetEnabled(value);
            OnPropertyChanged();
        }
    }

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

    public void Refresh()
    {
        OnPropertyChanged(nameof(StartupEnabled));
        OnPropertyChanged(nameof(StartMinimized));
        OnPropertyChanged(nameof(ShortcutStatsEnabled));
        OnPropertyChanged(nameof(ScreenPositionStatsEnabled));
        OnPropertyChanged(nameof(InputDiagnosticsEnabled));
        ReloadExcluded();
        ReloadInputDiagnostics();
    }

    [RelayCommand]
    private void AddExcluded()
    {
        var name = NewProcessName;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        _exclusions.Exclude(name);
        NewProcessName = string.Empty;
    }

    [RelayCommand]
    private void BrowseExcluded()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "应用程序|*.exe|所有文件|*.*",
            Title = "选择要排除的程序",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _exclusions.Exclude(dialog.FileName);
        NewProcessName = string.Empty;
    }

    [RelayCommand]
    private void RemoveExcluded(ExcludedAppRow? item)
    {
        if (item is null || !item.CanRemove)
        {
            return;
        }

        _exclusions.Remove(item.ProcessName);
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "选择导出目录" };
            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                return;
            }

            DataStatus = "正在导出…";
            await _flush.FlushNowAsync().ConfigureAwait(true);
            var files = await _export.ExportCsvAsync(dialog.FolderName).ConfigureAwait(true);
            DataStatus = "已导出 " + files.Count.ToString(System.Globalization.CultureInfo.CurrentCulture) +
                         " 个 CSV 到 " + dialog.FolderName;
        }
        catch (Exception ex)
        {
            DataStatus = string.Empty;
            WpfMessageBox.Show("导出失败：" + ex.Message, ProductInfo.Name, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            End();
        }
    }

    [RelayCommand]
    private async Task ClearStatsAsync()
    {
        var confirm = WpfMessageBox.Show(
            ClearConfirmText,
            "清空统计数据",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);
        if (confirm != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        if (!TryBegin())
        {
            return;
        }

        try
        {
            DataStatus = "正在清空…";
            await _flush.ClearStatisticsAsync().ConfigureAwait(true);
            DataStatus = "统计数据已清空。排除列表未改动。";
        }
        catch (Exception ex)
        {
            DataStatus = string.Empty;
            WpfMessageBox.Show("清空失败：" + ex.Message, ProductInfo.Name, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            End();
        }
    }

    [RelayCommand]
    private async Task ClearPositionDataAsync()
    {
        var confirm = WpfMessageBox.Show(
            "将删除点击位置、轨迹热力图和像素覆盖数据，不影响基础键鼠次数。此操作不能撤销。",
            "清除位置数据",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);
        if (confirm != System.Windows.MessageBoxResult.Yes || !TryBegin()) return;
        try
        {
            DataStatus = "正在清除位置数据…";
            await _flush.ClearPositionDataAsync().ConfigureAwait(true);
            DataStatus = "位置数据已清除。";
        }
        catch (Exception ex)
        {
            DataStatus = string.Empty;
            WpfMessageBox.Show("清除失败：" + ex.Message, ProductInfo.Name,
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            End();
        }
    }

    [RelayCommand]
    private void ClearInputDiagnostics() => _inputDiagnostics.Clear();

    private void ReloadExcluded()
    {
        ExcludedApps.Clear();
        foreach (var entry in _exclusions.Entries())
        {
            ExcludedApps.Add(new ExcludedAppRow(entry.ProcessName, entry.IsDefault));
        }
    }

    private void OnInputDiagnosticsChanged()
    {
        if (_dispatcher.CheckAccess())
        {
            ReloadInputDiagnostics();
            OnPropertyChanged(nameof(InputDiagnosticsEnabled));
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            ReloadInputDiagnostics();
            OnPropertyChanged(nameof(InputDiagnosticsEnabled));
        });
    }

    private void ReloadInputDiagnostics()
    {
        InputDiagnosticRows.Clear();
        foreach (var entry in _inputDiagnostics.Snapshot())
        {
            InputDiagnosticRows.Add(entry);
        }
    }

    private bool TryBegin()
    {
        lock (_gate)
        {
            if (_busy)
            {
                return false;
            }

            _busy = true;
            return true;
        }
    }

    private void End()
    {
        lock (_gate)
        {
            _busy = false;
        }
    }

    private void OnThemeChanged()
    {
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsSystemTheme));
    }
}

public sealed class ExcludedAppRow
{
    public ExcludedAppRow(string processName, bool isDefault)
    {
        ProcessName = processName;
        IsDefault = isDefault;
    }

    public string ProcessName { get; }

    public bool IsDefault { get; }

    public bool CanRemove => !IsDefault;

    public string StatusText => IsDefault ? "默认" : "";
}
