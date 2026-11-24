using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
using VisualPing.Models;
using VisualPing.Services;
using VisualPing.ViewModels;

namespace VisualPing;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs startupArguments)
    {
        base.OnStartup(startupArguments);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = startupArguments.Args
        });

        builder.Services.AddSingleton<IPingHistory>(_ => new PingHistory(50_000));
        builder.Services.AddSingleton<IPingMonitorService, PingMonitorService>();
        builder.Services.AddSingleton<ISettingsService, WpfSettingsService>();
        builder.Services.AddSingleton<IApplicationService, WpfApplicationService>();
        builder.Services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        builder.Services.AddSingleton<MainWindowViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();

        _host.Start();
        _host.Services.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs exitArguments)
    {
        _host?.StopAsync().GetAwaiter().GetResult();
        _host?.Dispose();
        base.OnExit(exitArguments);
    }
}
