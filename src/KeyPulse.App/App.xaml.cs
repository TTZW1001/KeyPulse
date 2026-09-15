using System.Windows;
using System.Windows.Threading;
using KeyPulse.App.Services;
using KeyPulse.App.ViewModels;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure;
using KeyPulse.Infrastructure.Logging;
using KeyPulse.Infrastructure.Persistence;
using KeyPulse.Infrastructure.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace KeyPulse.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private ISingleInstanceService? _singleInstance;
    private TrayService? _tray;
    private ApplicationLifecycleService? _lifecycle;
    private PowerEventService? _power;
    private ThemeService? _theme;

    protected override async void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.TryAcquire())
        {
            _singleInstance.SignalShowWindow();
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        var paths = new AppPaths();
        Log.Logger = SerilogSetup.Create(paths.LogsDirectory).CreateLogger();

        try
        {
            System.Windows.Forms.Application.EnableVisualStyles();

            _host = Host.CreateDefaultBuilder()
                .UseContentRoot(AppContext.BaseDirectory)
                .UseSerilog()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_singleInstance);
                    services.AddKeyPulseInfrastructure();
                    services.AddHostedService(sp => sp.GetRequiredService<FlushService>());
                    services.AddSingleton<ApplicationLifecycleService>();
                    services.AddSingleton<IApplicationLifecycle>(sp => sp.GetRequiredService<ApplicationLifecycleService>());
                    services.AddSingleton<TrayService>();
                    services.AddSingleton<PowerEventService>();
                    services.AddSingleton<ThemeService>();
                    services.AddSingleton<DashboardViewModel>();
                    services.AddSingleton<KeyboardViewModel>();
                    services.AddSingleton<MouseViewModel>();
                    services.AddSingleton<TrendsViewModel>();
                    services.AddSingleton<AppsViewModel>();
                    services.AddSingleton<SettingsViewModel>();
                    services.AddSingleton<MainWindowViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            await _host.StartAsync();
            await _host.Services.GetRequiredService<IAppHost>().StartAsync();
            _host.Services.GetRequiredService<IStatisticsAggregator>();
            var capture = _host.Services.GetRequiredService<IInputCapture>();
            await capture.StartAsync();
            if (capture.Error is not null)
            {
                _host.Services.GetRequiredService<IStatisticsAggregator>()
                    .SetState(TrackingState.Error);
            }

            _theme = _host.Services.GetRequiredService<ThemeService>();
            _theme.Initialize();

            var window = _host.Services.GetRequiredService<MainWindow>();
            _lifecycle = _host.Services.GetRequiredService<ApplicationLifecycleService>();
            _lifecycle.Attach(window);
            _tray = _host.Services.GetRequiredService<TrayService>();
            _power = _host.Services.GetRequiredService<PowerEventService>();
            _singleInstance.StartShowListener(() => Dispatcher.Invoke(() => _lifecycle.ShowMainWindow()));

            if (!LaunchArguments.IsSilentStartup(e.Args))
            {
                window.Show();
            }
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
                await _host.Services.GetRequiredService<IFlushService>().FlushNowAsync();
                await _host.Services.GetRequiredService<IAppHost>().StopAsync();
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _tray?.Dispose();
                _power?.Dispose();
                _theme?.Dispose();
                _lifecycle?.Dispose();
                _host.Dispose();
            }
        }
        finally
        {
            _singleInstance?.Dispose();
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception");
    }
}
