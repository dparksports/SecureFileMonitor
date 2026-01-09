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
            try
            {
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.File("logs/startup_log.txt")
                    .CreateLogger();

                Log.Information("Starting Host...");

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
                    services.AddSingleton<IAiService, SecureFileMonitor.Core.Services.AiService>();
                    services.AddSingleton<IFileScannerService, SecureFileMonitor.Core.Services.FileScannerService>();

                    // ViewModels
                    services.AddSingleton<SecureFileMonitor.UI.ViewModels.MainViewModel>();

                    // Views
                    services.AddSingleton<MainWindow>();
                })
                .Build();

                Log.Information("Host Built. Starting...");
                await _host.StartAsync();
                Log.Information("Host Started.");

                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                this.MainWindow = mainWindow; // Tell WPF this is the main window
                Log.Information("Showing MainWindow...");
                mainWindow.Show();

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application Startup Failed");
                MessageBox.Show($"Startup Error: {ex.Message}\n\n{ex.StackTrace}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
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
