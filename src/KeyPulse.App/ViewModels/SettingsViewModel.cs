using System.Collections.ObjectModel;
using System.IO;
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
    private readonly IDataMaintenanceService _maintenance;
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
        InputDiagnostics inputDiagnostics,
        IDataMaintenanceService maintenance)
    {
        _theme = theme;
        _startup = startup;
        _exclusions = exclusions;
        _flush = flush;
        _export = export;
        _settings = settings;
        _inputDiagnostics = inputDiagnostics;
        _maintenance = maintenance;
        _dispatcher = Dispatcher.CurrentDispatcher;
        DataPath = paths.DataDirectory;
        DatabasePath = paths.DatabasePath;
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

    public string DatabasePath { get; }

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

    [ObservableProperty]
    private string _databaseSizeText = "正在读取…";

    [ObservableProperty]
    private string _dateRangeText = "—";

    [ObservableProperty]
    private string _lastFlushText = "—";

    [ObservableProperty]
    private string _writeHealthText = "正常";

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

    public bool ShowInsights
    {
        get => _settings.ShowInsights;
        set
        {
            if (_settings.ShowInsights == value) return;
            _settings.ShowInsights = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public bool IsRetention30Days
    {
        get => _settings.PositionRetentionDays == 30;
        set { if (value) SetRetention(30); }
    }

    public bool IsRetention90Days
    {
        get => _settings.PositionRetentionDays == 90;
        set { if (value) SetRetention(90); }
    }

    public bool IsRetention365Days
    {
        get => _settings.PositionRetentionDays == 365;
        set { if (value) SetRetention(365); }
    }

    public bool IsRetentionForever
    {
        get => _settings.PositionRetentionDays <= 0;
        set { if (value) SetRetention(0); }
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
        OnPropertyChanged(nameof(ShowInsights));
        ReloadExcluded();
        ReloadInputDiagnostics();
        _ = RefreshDataStatusAsync();
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
    private async Task ClearRangeAsync(string? range)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var from = string.Equals(range, "today", StringComparison.Ordinal) ? today : today.AddDays(-29);
        var label = from == today ? "今天" : "最近 30 天";
        var confirm = WpfMessageBox.Show(
            $"将删除{label}的键鼠、快捷键、应用及有日期的位置明细；累计像素覆盖不受影响。此操作不能撤销。",
            "按范围清除", System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning, System.Windows.MessageBoxResult.No);
        if (confirm != System.Windows.MessageBoxResult.Yes || !TryBegin()) return;
        try
        {
            await _flush.ClearStatisticsRangeAsync(from, today).ConfigureAwait(true);
            DataStatus = $"{label}的统计数据已清除。";
            await RefreshDataStatusAsync();
        }
        catch (Exception ex) { DataStatus = "清除失败：" + ex.Message; }
        finally { End(); }
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
    private async Task BackupAsync()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "选择备份保存目录" };
        if (dialog.ShowDialog() != true || !TryBegin()) return;
        try
        {
            DataStatus = "正在创建一致性备份…";
            await _flush.FlushNowAsync().ConfigureAwait(true);
            string? archive = null;
            await _flush.RunExclusiveAsync(async ct =>
                archive = await _maintenance.CreateBackupAsync(dialog.FolderName, ct)).ConfigureAwait(true);
            DataStatus = "备份已保存：" + archive;
        }
        catch (Exception ex)
        {
            DataStatus = "备份失败：" + ex.Message;
        }
        finally { End(); }
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择 KeyPulse 备份",
            Filter = "KeyPulse 备份|*.zip"
        };
        if (dialog.ShowDialog() != true) return;
        var confirm = WpfMessageBox.Show(
            "恢复前会自动备份当前数据。恢复成功后需要重启 KeyPulse 才能重新载入全部设置，是否继续？",
            "恢复备份", System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning, System.Windows.MessageBoxResult.No);
        if (confirm != System.Windows.MessageBoxResult.Yes || !TryBegin()) return;
        try
        {
            DataStatus = "正在校验并恢复备份…";
            await _flush.FlushNowAsync().ConfigureAwait(true);
            await _flush.RunExclusiveAsync(async ct =>
            {
                var safetyDirectory = Path.Combine(DataRoot, "backups-before-restore");
                await _maintenance.CreateBackupAsync(safetyDirectory, ct);
                await _maintenance.RestoreBackupAsync(dialog.FileName, ct);
            }).ConfigureAwait(true);
            DataStatus = "恢复完成。请重启 KeyPulse 以重新载入设置。";
        }
        catch (Exception ex)
        {
            DataStatus = "恢复失败，当前数据未主动清除：" + ex.Message;
        }
        finally { End(); }
    }

    [RelayCommand]
    private async Task VacuumAsync()
    {
        if (!TryBegin()) return;
        try
        {
            DataStatus = "正在整理数据库空闲空间…";
            await _flush.FlushNowAsync().ConfigureAwait(true);
            await _flush.RunExclusiveAsync(_maintenance.VacuumAsync).ConfigureAwait(true);
            DataStatus = "数据库整理完成。";
            await RefreshDataStatusAsync();
        }
        catch (Exception ex) { DataStatus = "整理失败：" + ex.Message; }
        finally { End(); }
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

    private void SetRetention(int days)
    {
        if (_settings.PositionRetentionDays == days) return;
        _settings.PositionRetentionDays = days;
        _settings.Save();
        OnPropertyChanged(nameof(IsRetention30Days));
        OnPropertyChanged(nameof(IsRetention90Days));
        OnPropertyChanged(nameof(IsRetention365Days));
        OnPropertyChanged(nameof(IsRetentionForever));
        _ = RefreshDataStatusAsync();
    }

    private async Task RefreshDataStatusAsync()
    {
        try
        {
            var status = await _maintenance.GetStatusAsync(
                _flush.LastSuccessfulFlush, _flush.HasWriteError).ConfigureAwait(false);
            await _dispatcher.InvokeAsync(() =>
            {
                DatabaseSizeText = FormatBytes(status.DatabaseBytes);
                DateRangeText = status.EarliestDate is null ? "暂无统计数据" :
                    $"{status.EarliestDate:yyyy-MM-dd} ～ {status.LatestDate:yyyy-MM-dd}";
                LastFlushText = status.LastSuccessfulFlush?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "本次启动尚未写入";
                WriteHealthText = status.HasWriteError ? "写入异常，正在重试" : "正常";
            });
        }
        catch (Exception ex)
        {
            await _dispatcher.InvokeAsync(() => WriteHealthText = "读取失败：" + ex.Message);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024L) return (bytes / 1024D / 1024D).ToString("0.0") + " MB";
        if (bytes >= 1024L) return (bytes / 1024D).ToString("0.0") + " KB";
        return bytes + " B";
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
