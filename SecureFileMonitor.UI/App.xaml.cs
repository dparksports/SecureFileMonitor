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
using System.Threading.Tasks; // Added for TaskScheduler

namespace SecureFileMonitor.UI
{
    public partial class App : Application
    {
        private static IHost? _host;
        public static IHost Host => _host ?? throw new InvalidOperationException("Host not initialized");

        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "UI Thread Exception");
            LogToCrashFile(e.Exception, "UI_Thread_Crash");
            MessageBox.Show($"Unexpected UI Error: {e.Exception.Message}\n\nDetails saved to crash_log.txt", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // Prevent crash if possible
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
             var ex = e.ExceptionObject as Exception;
             if (ex != null)
             {
                 Log.Fatal(ex, "Domain Unhandled Exception");
                 LogToCrashFile(ex, "Fatal_Domain_Crash");
             }
             MessageBox.Show($"Critical Runtime Error: {ex?.Message}\n\nDetails saved to crash_log.txt", "Fatal Crash", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            // Log but don't show message box for unobserved tasks to avoid spam, unless critical
            Log.Error(e.Exception, "Unobserved Task Exception");
            LogToCrashFile(e.Exception, "Background_Task_Exception");
            e.SetObserved();
        }

        private void LogToCrashFile(Exception ex, string type)
        {
            try
            {
                string crashFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                string logContent = $"[{DateTime.Now}] [{type}]\nMessage: {ex.Message}\nStack Trace:\n{ex.StackTrace}\n\n--------------------------------------------------\n\n";
                File.AppendAllText(crashFile, logContent);
            }
            catch 
            {
                // Last resort: fails quietly if we can't even write to disk
            }
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
                    // services.AddSingleton<IHasherService, SecureFileMonitor.Core.Services.CpuHasherService>(); // Replaced by Factory
                    services.AddSingleton<SecureFileMonitor.Core.Services.HasherFactory>();
                    services.AddSingleton<IMerkleTreeService, SecureFileMonitor.Core.Services.MerkleTreeService>();
                    services.AddSingleton<IAiService, SecureFileMonitor.Core.Services.AiService>();
                    services.AddSingleton<IAnalyticsService, SecureFileMonitor.Core.Services.GoogleAnalyticsService>();
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
