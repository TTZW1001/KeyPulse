using System.Windows;
using System.Windows.Threading;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure;
using KeyPulse.Infrastructure.Logging;
using KeyPulse.Infrastructure.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace KeyPulse.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var paths = new AppPaths();
        Log.Logger = SerilogSetup.Create(paths.LogsDirectory).CreateLogger();

        try
        {
            _host = Host.CreateDefaultBuilder()
                .UseContentRoot(AppContext.BaseDirectory)
                .UseSerilog()
                .ConfigureServices(services =>
                {
                    services.AddKeyPulseInfrastructure();
                    services.AddSingleton<MainWindowViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            await _host.StartAsync();
            await _host.Services.GetRequiredService<IAppHost>().StartAsync();
            var capture = _host.Services.GetRequiredService<IInputCapture>();
            await capture.StartAsync();
            if (capture.Error is not null)
            {
                _host.Services.GetRequiredService<IStatisticsAggregator>()
                    .SetState(TrackingState.Error);
            }

            var window = _host.Services.GetRequiredService<MainWindow>();
            window.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "KeyPulse failed to start");
            Log.CloseAndFlush();
            throw;
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host is not null)
            {
                await _host.Services.GetRequiredService<IInputCapture>().StopAsync();
                await _host.Services.GetRequiredService<IAppHost>().StopAsync();
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _host.Dispose();
            }
        }
        finally
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception");
    }
}
