using Serilog;

namespace KeyPulse.Infrastructure.Logging;

public static class SerilogSetup
{
    public static LoggerConfiguration Create(string logsDirectory)
    {
        Directory.CreateDirectory(logsDirectory);

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(logsDirectory, "keypulse-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
    }
}
