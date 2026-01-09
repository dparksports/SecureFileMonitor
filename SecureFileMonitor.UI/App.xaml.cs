using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SecureFileMonitor.Core.Services;
using SecureFileMonitor.UI.ViewModels;

namespace SecureFileMonitor.UI
{
    public partial class App : Application
    {
        private static IHost? _host;
        public static IHost Host => _host ?? throw new InvalidOperationException("Host not initialized");

        public App()
        {
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            _host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .UseSerilog((context, services, configuration) => configuration
                    .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day))
                .ConfigureServices((context, services) =>
                {
                    // Services
                    services.AddSingleton<IUsnJournalService, SecureFileMonitor.Core.Services.UsnJournalService>();
                    services.AddSingleton<IEtwMonitorService, SecureFileMonitor.Core.Services.EtwMonitorService>();
                    services.AddSingleton<IDatabaseService, SecureFileMonitor.Core.Services.DatabaseService>();
                    services.AddSingleton<IHasherService, SecureFileMonitor.Core.Services.CpuHasherService>();
                    services.AddSingleton<IMerkleTreeService, SecureFileMonitor.Core.Services.MerkleTreeService>();

                    // ViewModels
                    services.AddSingleton<SecureFileMonitor.UI.ViewModels.MainViewModel>();

                    // Views
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }

            base.OnExit(e);
        }
    }
}
