using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KeyPulse.Infrastructure.Persistence;

public sealed class AutoBackupService : BackgroundService
{
    private readonly IUserSettings _settings;
    private readonly IAppPaths _paths;
    private readonly IFlushService _flush;
    private readonly IDataMaintenanceService _maintenance;
    private readonly ILogger<AutoBackupService> _logger;

    public AutoBackupService(
        IUserSettings settings,
        IAppPaths paths,
        IFlushService flush,
        IDataMaintenanceService maintenance,
        ILogger<AutoBackupService> logger)
    {
        _settings = settings;
        _paths = paths;
        _flush = flush;
        _maintenance = maintenance;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CheckAsync(stoppingToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await CheckAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        if (!_settings.AutoBackupEnabled) return;
        var interval = _settings.AutoBackupFrequency == BackupFrequency.Daily
            ? TimeSpan.FromDays(1)
            : TimeSpan.FromDays(7);
        if (_settings.LastAutoBackupAt is { } last && DateTimeOffset.Now - last < interval) return;

        try
        {
            await _flush.FlushNowAsync(cancellationToken).ConfigureAwait(false);
            var directory = _settings.AutoBackupDirectory ?? _paths.BackupsDirectory;
            await _flush.RunExclusiveAsync(async ct =>
            {
                await _maintenance.CreateBackupAsync(directory, ct).ConfigureAwait(false);
                Prune(directory, _settings.AutoBackupRetentionCount);
            }, cancellationToken).ConfigureAwait(false);
            _settings.LastAutoBackupAt = DateTimeOffset.Now;
            _settings.Save();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Automatic backup failed");
        }
    }

    private static void Prune(string directory, int keep)
    {
        if (!Directory.Exists(directory)) return;
        foreach (var file in new DirectoryInfo(directory)
                     .GetFiles("KeyPulse-backup-*.zip")
                     .OrderByDescending(item => item.CreationTimeUtc)
                     .Skip(Math.Clamp(keep, 1, 50)))
        {
            file.Delete();
        }
    }
}
