using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SecureFileMonitor.Core.Services;
using SecureFileMonitor.UI.ViewModels;
using System.Security.Principal;
using System.Diagnostics;
using System;

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
            if (!IsAdministrator())
            {
                var result = MessageBox.Show(
                    "This application requires Administrator privileges to monitor the USN Journal and file system events.\n\nWould you like to restart as Administrator?",
                    "Elevation Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    RestartAsAdmin();
                    return;
                }
            }

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

        private bool IsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private void RestartAsAdmin()
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule?.FileName,
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                Process.Start(processInfo);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to restart as administrator");
                MessageBox.Show("Could not restart with administrator privileges. Please right-click the application and select 'Run as administrator'.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
