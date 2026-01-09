using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureFileMonitor.Core.Models;
using SecureFileMonitor.Core.Services;
using System.Threading.Tasks;

namespace SecureFileMonitor.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IUsnJournalService _usnService;
        private readonly IEtwMonitorService _etwService;
        private readonly IDatabaseService _dbService;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _isMonitoring;

        public ObservableCollection<FileActivityEvent> RecentActivity { get; } = new();
        public ObservableCollection<FileEntry> DuplicateFiles { get; } = new();

        public MainViewModel(IUsnJournalService usnService, IEtwMonitorService etwService, IDatabaseService dbService)
        {
            _usnService = usnService;
            _etwService = etwService;
            _dbService = dbService;

            // Wire up events
            _etwService.OnFileActivity += EtwService_OnFileActivity;
        }

        private void EtwService_OnFileActivity(object? sender, FileActivityEvent e)
        {
            // Marshal to UI thread if needed (WPF usually needs Dispatcher, but ObservableCollection might need it)
            // For now, assume ViewModel needs to handle dispatching or use BindingOperations.EnableCollectionSynchronization
            
            App.Current.Dispatcher.Invoke(() =>
            {
                RecentActivity.Insert(0, e);
                if (RecentActivity.Count > 100) RecentActivity.RemoveAt(100);
            });
            
            // Log to DB
            Task.Run(async () => await _dbService.SaveAuditLogAsync(e));
        }

        [RelayCommand]
        public async Task ScanDuplicates()
        {
            StatusMessage = "Scanning for duplicates...";
            var dupeList = await _dbService.GetDuplicateFilesAsync();
            
            DuplicateFiles.Clear();
            foreach (var file in dupeList)
            {
                DuplicateFiles.Add(file);
            }
            StatusMessage = $"Found {DuplicateFiles.Count} duplicate candidates.";
        }

        [RelayCommand]
        public async Task StartMonitoring()
        {
            StatusMessage = "Initializing Database...";
            await _dbService.InitializeAsync("SecurePassword123!"); // User should input this

            StatusMessage = "Starting USN Journal Reader...";
            await Task.Run(() => _usnService.Initialize("C:"));
            // Start reading history...

            StatusMessage = "Starting Real-time ETW Monitor...";
            _etwService.Start();

            IsMonitoring = true;
            StatusMessage = "Monitoring Active - System Secure";
        }

        [RelayCommand]
        public void StopMonitoring()
        {
            _etwService.Stop();
            IsMonitoring = false;
            StatusMessage = "Monitoring Stopped";
        }
    }
}
