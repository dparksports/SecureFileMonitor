using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureFileMonitor.Core.Models;
using SecureFileMonitor.Core.Services;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SecureFileMonitor.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IUsnJournalService _usnService;
        private readonly IEtwMonitorService _etwService;
        private readonly IDatabaseService _dbService;
        private readonly IAiService _aiService;
        private readonly IAnalyticsService _analyticsService;
        private readonly IFileScannerService _scannerService;
        private CancellationTokenSource? _scanCts;
        private System.Timers.Timer _eventProcessingTimer;
        private readonly System.Collections.Concurrent.ConcurrentQueue<FileActivityEvent> _eventQueue = new();

        public ObservableCollection<string> IgnoredProcesses { get; } = new();
        public ObservableCollection<TranscriptionTask> FilteredTranscribedFiles { get; } = new();

        [ObservableProperty]
        private bool _showIgnoredProcesses;

        [ObservableProperty]
        private FileEntry? _selectedFile;

        [ObservableProperty]
        private DateTime? _transcribedDateFilter;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _isMonitoring;

        [ObservableProperty]
        private bool _monitorInternalDatabase;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        partial void OnMonitorInternalDatabaseChanged(bool value)
        {
             _etwService.IgnoreInternalDatabase = !value;
        }

        public ObservableCollection<FileActivityEvent> RecentActivity { get; } = new();
        public ObservableCollection<FileActivityEvent> OfflineActivity { get; } = new();
        public ObservableCollection<FileEntry> DuplicateFiles { get; } = new();
        public ObservableCollection<FileEntry> SearchResults { get; } = new();
        public ObservableCollection<FileEntry> AllFiles { get; } = new();
        public ObservableCollection<FileEntry> FilteredFiles { get; } = new();
        public ObservableCollection<TranscriptionTask> TranscribeQueue { get; } = new();

        [ObservableProperty]
        private TranscriptionTask? _selectedTranscriptionTask;

        public ObservableCollection<IgnoreRule> CurrentIgnoreRules { get; } = new();
        
        [ObservableProperty]
        private bool _scanReparseFolders = true;

        [ObservableProperty]
        private bool _enableIgnoreList = false;

        [ObservableProperty]
        private string _currentViewName = "VAULT"; // Default to Vault as requested

        // Statistics
        [ObservableProperty]
        private int _totalFiles;

        [ObservableProperty]
        private int _totalVideoFiles;

        [ObservableProperty]
        private int _totalAudioFiles;

        [ObservableProperty]
        private int _totalCodeFiles;

        [ObservableProperty]
        private string _lastScannedTime = "Never";

        [ObservableProperty]
        private string _scanETA = "";

        // Background Hashing Progress
        [ObservableProperty]
        private string _hashProgress = "";

        [ObservableProperty]
        private string _hashETA = "";

        // Sorting
        [ObservableProperty]
        private string _sortBy = "LastModified"; // Default to LastModified to show actual work first
        
        public ObservableCollection<string> SortByOptions { get; } = new() { "LastModified", "Size", "Name", "Type" };

        // File Type Filtering
        [ObservableProperty]
        private string _fileTypeFilter = "All"; // All, Audio, Video, Office, PDF, Text, Image
        
        public ObservableCollection<string> FileTypeOptions { get; } = new() { "All", "Audio", "Video", "Images", "Documents", "Archives", "Code" };

        // Date Filtering
        [ObservableProperty]
        private DateTime? _dateFilterStart;

        [ObservableProperty]
        private DateTime? _dateFilterEnd;

        [ObservableProperty]
        private int _selectedItemsCount;

        [ObservableProperty]
        private long _selectedItemsSize;

        [ObservableProperty]
        private bool _isTranscribeViewVisible;

        [ObservableProperty]
        private bool _isFileDetailsVisible;

        [ObservableProperty]
        private bool _isLiveActivityPaused;

        [RelayCommand]
        private void ToggleLiveActivityPause()
        {
            IsLiveActivityPaused = !IsLiveActivityPaused;
        }

        [ObservableProperty]
        private int _maxLiveItems = 50;

        [ObservableProperty]
        private int _maxOfflineItems = 50;

        async partial void OnMaxOfflineItemsChanged(int value)
        {
            await LoadOfflineHistory();
        }

        [RelayCommand]
        public async Task LoadOfflineHistory()
        {
             var history = await _dbService.GetRecentOfflineActivityAsync(MaxOfflineItems);
             System.Windows.Application.Current.Dispatcher.Invoke(() =>
             {
                 OfflineActivity.Clear();
                 foreach (var evt in history)
                 {
                     OfflineActivity.Add(evt);
                 }
             });
        }

        // Settings & About
        public string AppVersion => "v1.3.3";

        [ObservableProperty]
        private bool _isAnalyticsEnabled = true;

        async partial void OnIsAnalyticsEnabledChanged(bool value)
        {
            _analyticsService.IsEnabled = value;
            await _dbService.SaveSettingAsync("IsAnalyticsEnabled", value.ToString());
        }

        // EULA
        [ObservableProperty]
        private bool _isEulaVisible;

        public string EulaText => "SECURE FILE MONITOR END USER LICENSE AGREEMENT\n\n1. GRANT OF LICENSE\nThis application is provided 'as is' for personal and commercial use.\n\n2. DATA PRIVACY\nWe respect your privacy. All processing is local. Optional analytics are collected anonymously.\n\n3. DISCLAIMER\nThe authors are not liable for any damages arising from the use of this software.";

        [RelayCommand]
        private async Task AcceptEula()
        {
            await _dbService.SaveSettingAsync("EulaAccepted", "true");
            IsEulaVisible = false;
        }

        [RelayCommand]
        private void DeclineEula()
        {
            System.Windows.Application.Current.Shutdown();
        }

        [ObservableProperty]
        private bool _showDllEvents = false; // Default Off

        [ObservableProperty]
        private bool _showExeEvents = true; // Default On (Usually interesting)

        [ObservableProperty]
        private bool _showMuiEvents = false; // Default Off

        [ObservableProperty]
        private FileActivityEvent? _selectedOfflineEvent;

        [RelayCommand]
        public void ShowOfflineEventDetails()
        {
            if (SelectedOfflineEvent == null) return;
            
            _analyticsService.LogEventAsync("view_offline_details");

            System.Windows.MessageBox.Show(
                $"File: {SelectedOfflineEvent.FilePath}\n\nChange Details:\n{SelectedOfflineEvent.Details}", 
                "Offline Change Analysis", 
                System.Windows.MessageBoxButton.OK, 
                System.Windows.MessageBoxImage.Information);
        }

        [ObservableProperty]
        private bool _showPsd1Events = false; // Default Off

        [ObservableProperty]
        private bool _showPowerShellEvents = false; // Default: Off (Filters \Windows\System32\WindowsPowerShell\)

        [ObservableProperty]
        private bool _showSystem32Events = false; // Default: Off (Filters \Windows\System32\)

        [ObservableProperty]
        private bool _showPnfEvents = false; // Default: Off (Filters .pnf)

        [ObservableProperty]
        private bool _showWindowsEvents = false; // Default: Off (Filters \Windows\)

        [ObservableProperty]
        private string _transcribedFileSearchQuery = string.Empty;

        [ObservableProperty]
        private string _selectedWhisperModel = "Base"; // Base, Medium, Large, Turbo

        public System.Collections.Generic.List<string> WhisperModels { get; } = new() { "Base", "Medium", "Large", "Turbo" };

        [ObservableProperty]
        private bool _isEnglishOnly = true;

        [ObservableProperty]
        private bool _useGpu = false;

        [ObservableProperty]
        private bool _useThreads = false;

        [ObservableProperty]
        private bool _verifyGpuHash = false; // Default off, requires UseGpu

        // GPU Availability
        [ObservableProperty]
        private bool _isGpuAvailable; // Set in constructor

        [ObservableProperty]
        private string _gpuCheckboxContent = "Use GPU Acceleration (Experimental)";

        public bool IsGpuSelectionEnabled => !IsScanning && IsGpuAvailable;

        partial void OnIsScanningChanged(bool value)
        {
            OnPropertyChanged(nameof(IsGpuSelectionEnabled));
        }

        async partial void OnUseGpuChanged(bool value)
        {
            try { await _dbService.SaveSettingAsync("UseGpu", value.ToString()); } catch { /* Ignore or Log */ }
        }

        async partial void OnUseThreadsChanged(bool value)
        {
             try { await _dbService.SaveSettingAsync("UseThreads", value.ToString()); } catch { /* Ignore or Log */ }
        }

        async partial void OnVerifyGpuHashChanged(bool value)
        {
             try { await _dbService.SaveSettingAsync("VerifyGpuHash", value.ToString()); } catch { /* Ignore or Log */ }
        }

        [ObservableProperty]
        private bool _isCudaAvailable;
        
        // Drive Selection
        public ObservableCollection<DriveViewModel> AvailableDrives { get; } = new();
        
        [RelayCommand]
        public async Task LoadDrives()
        {
             var drives = DriveInfo.GetDrives().Where(d => d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable));
             AvailableDrives.Clear();
             
             // Restore previous selection if possible? For now default all true.
             // Or load from DB (SavedSelectedDrives).
             // Implementing basic load for now.
             
             foreach (var d in drives)
             {
                 AvailableDrives.Add(new DriveViewModel(d));
             }
        }
        
        [ObservableProperty]
        private bool _isPaused = false;
        
        [RelayCommand]
        public async Task ScanNewFiles()
        {
             await RunScan(false);
        }

        [RelayCommand]
        public async Task FullScan()
        {
             await RunScan(true);
        }

        [RelayCommand]
        public void StopScan()
        {
            if (_scanCts != null)
            {
                try { _scanCts.Cancel(); } catch {}
                StatusMessage = "Stopping scan...";
            }
        }

        [RelayCommand]
        public async Task PauseScan()
        {
             if (_scanCts != null && !_scanCts.IsCancellationRequested)
             {
                 _scannerService.SetPaused(true);
                 IsPaused = true;
                 StatusMessage = "Pausing scan...";
                 try { await _dbService.SaveSettingAsync("IsPaused", "true"); } catch {}
             }
        }

        [RelayCommand]
        public async Task ResumeScan()
        {
             _scannerService.SetPaused(false);
             IsPaused = false;
             // Clear pause state
             try { await _dbService.SaveSettingAsync("IsPaused", "false"); } catch {}
             
             await RunScan(false); // Resume effectively means run again, picking up queue
        }

        [RelayCommand]
        public async Task LoadAllFiles()
        {
             StatusMessage = "Loading files...";
             // Simple implementation to satisfy build and functionality
             var files = await _dbService.GetAllFilesAsync();
             
             System.Windows.Application.Current.Dispatcher.Invoke(() =>
             {
                 AllFiles.Clear();
                 foreach(var f in files) AllFiles.Add(f);
                 
                 FilteredFiles.Clear();
                 foreach(var f in AllFiles) FilteredFiles.Add(f);
             });
             
             UpdateSelectionTally(null); 
             StatusMessage = $"Loaded {AllFiles.Count} files.";
        }

        // ...

        [ObservableProperty]
        private bool _isScanning;

        // ...

        private async Task RunScan(bool isFullScan)
        {
            // Reset CTS
            if (_scanCts != null) {  try { _scanCts.Cancel(); _scanCts.Dispose(); } catch {} }
            _scanCts = new CancellationTokenSource();
            _scannerService.SetPaused(false);
            IsPaused = false;
            IsScanning = true;

            try
            {
                StatusMessage = "Initializing database...";
                await _dbService.InitializeAsync("SecurePassword123!");
                
                // Load Settings
                string? useGpuVal = await _dbService.GetSettingAsync("UseGpu");
                string? useThreadsVal = await _dbService.GetSettingAsync("UseThreads");
                string? verifyVal = await _dbService.GetSettingAsync("VerifyGpuHash");
                string? isPausedVal = await _dbService.GetSettingAsync("IsPaused");

                UseGpu = bool.TryParse(useGpuVal, out bool g) && g;
                UseThreads = bool.TryParse(useThreadsVal, out bool t) && t;
                VerifyGpuHash = bool.TryParse(verifyVal, out bool v) && v;
                
                // Restore Pause State
                if (bool.TryParse(isPausedVal, out bool p) && p)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                    {
                        IsPaused = true;
                        IsScanning = true; // Required to show Resume button
                        StatusMessage = "Scan Paused (Resumable)";
                    });
                }

                string modeName = isFullScan ? "FULL SCAN" : "SCAN NEW FILES";
                
                // Get Selected Drives
                if (AvailableDrives.Count == 0) await LoadDrives();
                var selectedDrives = AvailableDrives.Where(d => d.IsSelected).ToList();
                
                if (selectedDrives.Count == 0)
                {
                    StatusMessage = "No drives selected.";
                    IsScanning = false;
                    return;
                }

                StatusMessage = $"{modeName}: Scanning {selectedDrives.Count} drives...";
                
                var progress = new Progress<(string Status, double? Percent, TimeSpan? ETA)>(val => 
                {
                     StatusMessage = val.Status;
                     if (val.ETA.HasValue)
                     {
                        ScanETA = $"ETA: {val.ETA.Value.Hours:D2}:{val.ETA.Value.Minutes:D2}:{val.ETA.Value.Seconds:D2}";
                     }
                     else 
                     {
                        ScanETA = "";
                     }
                });

                foreach (var drive in selectedDrives)
                {
                    if (_scanCts.Token.IsCancellationRequested || IsPaused) break;

                    StatusMessage = $"{modeName}: Scanning {drive.Name}...";
                    await _scannerService.ScanDriveAsync(drive.Name, ScanReparseFolders, isFullScan, progress, _scanCts.Token);
                }


                if (IsPaused)
                {
                    StatusMessage = "Scan Paused.";
                    // Keep IsScanning = true so we can Resume
                }
                else if (_scanCts.Token.IsCancellationRequested)
                {
                    StatusMessage = "Scan Stopped.";
                    IsScanning = false;
                }
                else
                {
                    StatusMessage = "Scan Complete. Starting background hash...";
                    ScanETA = ""; // Clear ETA
                    await LoadAllFiles(); // Auto-refresh
                    
                    // Start background hashing for large files
                    int pendingHashes = _scannerService.GetPendingHashCount();
                    if (pendingHashes > 0 && !_scanCts.Token.IsCancellationRequested)
                    {
                        HashProgress = $"Hashing {pendingHashes} large files...";
                        var hashProgress = new Progress<(string Status, int Remaining, int Total, TimeSpan? ETA)>(val =>
                        {
                            HashProgress = $"Hashing: {val.Status} ({val.Remaining}/{val.Total})";
                            if (val.ETA.HasValue)
                            {
                                HashETA = $"ETA: {val.ETA.Value.Hours:D2}:{val.ETA.Value.Minutes:D2}:{val.ETA.Value.Seconds:D2}";
                            }
                        });
                        await _scannerService.StartBackgroundHashingAsync(hashProgress, _scanCts.Token);
                        HashProgress = "";
                        HashETA = "";
                        StatusMessage = "Scan and Hash Complete.";
                    }
                    IsScanning = false;
                }
            }

            catch (OperationCanceledException)
            {
                StatusMessage = "Scan Stopped.";
                ScanETA = "";
                IsScanning = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Scan Error: {ex.Message}";
                ScanETA = "";
                IsScanning = false;
            }

        }

        [RelayCommand]
        private async Task IgnoreSelectedFiles(System.Collections.IList? selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0) return;

            foreach (var item in selectedItems.Cast<FileEntry>().ToList())
            {
                var rule = new IgnoreRule { Path = item.FilePath, IsDirectory = false };
                await _dbService.AddIgnoreRuleAsync(rule);
            }


            await LoadAllFiles(); // Refresh Vault to hide ignored files
            await RefreshIgnoreList(); // Update Ignore List view
            StatusMessage = $"Ignored {selectedItems.Count} files.";
        }

        [RelayCommand]
        private async Task UnignoreSelectedFiles(System.Collections.IList? selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0) return;

            foreach (var item in selectedItems.Cast<FileEntry>().ToList())
            {
                 await _dbService.RemoveIgnoreRuleAsync(item.FilePath);
            }


            await LoadAllFiles(); 
            await RefreshIgnoreList(); 
            StatusMessage = $"Unignored {selectedItems.Count} files.";
        }

        [RelayCommand]
        private async Task RefreshIgnoreList()
        {
            var rules = await _dbService.GetAllIgnoreRulesAsync();
            CurrentIgnoreRules.Clear();
            foreach (var rule in rules) CurrentIgnoreRules.Add(rule);
            OnPropertyChanged(nameof(IgnoredGroups));
        }

        public class IgnoreGroup
        {
            public string ParentPath { get; set; } = string.Empty;
            public List<IgnoreRule> Rules { get; set; } = new();
            public List<string> ParentHierarchy { get; set; } = new();
        }

        public List<IgnoreGroup> IgnoredGroups
        {
            get
            {
                var groupsList = new List<IgnoreGroup>();
                var groupsDict = new Dictionary<string, IgnoreGroup>();
                foreach (var rule in CurrentIgnoreRules)
                {
                    string parent = rule.IsDirectory ? rule.Path : (Path.GetDirectoryName(rule.Path) ?? rule.Path);
                    if (!groupsDict.ContainsKey(parent))
                    {
                        var group = new IgnoreGroup { ParentPath = parent };
                        string? current = parent;
                        int depth = 0;
                        while (!string.IsNullOrEmpty(current) && depth < 5)
                        {
                            group.ParentHierarchy.Add(current);
                            current = Path.GetDirectoryName(current);
                            depth++;
                        }

                        groupsDict[parent] = group;
                        groupsList.Add(group);
                    }

                    groupsDict[parent].Rules.Add(rule);
                }
                return groupsList;

            }

        }

        [RelayCommand]
        private async Task PromoteIgnoreRule(IgnoreGroup group)
        {
            // This is called when user clicks a parent in hierarchy
            // For now, let's just show a simple way to expand in UI
            // Implementation details depend on how user selects the new parent
        }

        [RelayCommand]
        private async Task UnignoreRule(IgnoreRule rule)
        {
            await _dbService.RemoveIgnoreRuleAsync(rule.Path);
            await RefreshIgnoreList();
            await LoadAllFiles();
        }

        [RelayCommand]
        private async Task UnignoreGroup(IgnoreGroup group)
        {
            foreach (var rule in group.Rules)
            {
                await _dbService.RemoveIgnoreRuleAsync(rule.Path);
            }

            await RefreshIgnoreList();
            await LoadAllFiles();
        }

        [RelayCommand]
        private async Task ReplaceWithParentIgnore(string newParentPath)
        {
            // Find all rules that are children of this parent
            var rules = await _dbService.GetAllIgnoreRulesAsync();
            var children = rules.Where(r => r.Path.StartsWith(newParentPath, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var child in children)
            {
                await _dbService.RemoveIgnoreRuleAsync(child.Path);
            }


            await _dbService.AddIgnoreRuleAsync(new IgnoreRule { Path = newParentPath, IsDirectory = true });
            await RefreshIgnoreList();
            await LoadAllFiles();
            StatusMessage = $"Ignoring directory: {newParentPath}";
        }

        [RelayCommand]
        private void UpdateSelectionTally(System.Collections.IList? selectedItems)
        {
            if (selectedItems == null)
            {
                SelectedItemsCount = 0;
                SelectedItemsSize = 0;
                return;

            }


            var items = selectedItems.Cast<FileEntry>().ToList();
            SelectedItemsCount = items.Count;
            SelectedItemsSize = items.Sum(f => f.FileSize);
        }

        [RelayCommand]
        private async Task TranscribeSelectedFiles(System.Collections.IList? selectedItems)
        {
            if (selectedItems == null) return;
            var files = selectedItems.Cast<FileEntry>().ToList();
            
            foreach (var file in files)
            {
                // Check if already in queue or transcribed
                if (TranscribeQueue.Any(t => t.FilePath == file.FilePath)) continue;

                var task = new TranscriptionTask
                {
                    FilePath = file.FilePath,
                    FileName = file.FileName,
                    Status = TranscriptionStatus.Queued,
                    QueuedAt = DateTime.Now
                };
                TranscribeQueue.Add(task);
                await _dbService.SaveTranscriptionTaskAsync(task); // Persist new task
            }

            _ = ProcessTranscriptionQueueAsync();
            CurrentViewName = "TRANSCRIBE";
        }

        [RelayCommand]
        private async Task ReTranscribeSelectedTask()
        {
            if (SelectedTranscriptionTask == null) return;
            
            // Reset the task for re-processing with new model settings
            string modelLabel = $"Whisper {SelectedWhisperModel}" + (IsEnglishOnly ? " (English)" : " (Multilingual)");
            
            // Store previous transcript for comparison
            string previousTranscript = SelectedTranscriptionTask.Transcript;
            string previousModel = SelectedTranscriptionTask.ModelUsed;
            
            SelectedTranscriptionTask.Status = TranscriptionStatus.Queued;
            SelectedTranscriptionTask.ModelUsed = modelLabel;
            SelectedTranscriptionTask.Transcript = $"[Previous: {previousModel}]\n{previousTranscript}\n\n[Re-transcribing with {modelLabel}...]";
            
            await _dbService.SaveTranscriptionTaskAsync(SelectedTranscriptionTask); // Update task status
            _ = ProcessTranscriptionQueueAsync();
        }

        [RelayCommand]
        private async Task IgnoreProcess(string? processName)
        {
            if (string.IsNullOrEmpty(processName)) return;
            if (!IgnoredProcesses.Contains(processName))
            {
                IgnoredProcesses.Add(processName);
                await _dbService.AddIgnoredProcessAsync(processName);
                
                // Remove existing events for this process from the current view
                var toRemove = RecentActivity.Where(a => a.ProcessName == processName).ToList();
                foreach (var item in toRemove) RecentActivity.Remove(item);
                
                // Clear from active events dictionary too
                var keysToRemove = _activeFileEvents.Where(kv => kv.Value.ProcessName == processName).Select(kv => kv.Key).ToList();
                foreach (var key in keysToRemove) _activeFileEvents.Remove(key);
                
                StatusMessage = $"Ignoring process: {processName}";
            }
        }

        [RelayCommand]
        private async Task RemoveIgnoredProcess(string? processName)
        {
            if (string.IsNullOrEmpty(processName)) return;
            if (IgnoredProcesses.Contains(processName))
            {
                IgnoredProcesses.Remove(processName);
                await _dbService.RemoveIgnoredProcessAsync(processName);
                StatusMessage = $"Restored process: {processName}";
            }
        }

        [RelayCommand]
        private void ToggleIgnoredProcessesPanel()
        {
            ShowIgnoredProcesses = !ShowIgnoredProcesses;
        }

        private void ApplyTranscriptionFilters()
        {
            var query = TranscribeQueue.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(TranscribedFileSearchQuery))
            {
                query = query.Where(t => 
                    (t.FileName?.Contains(TranscribedFileSearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (t.Transcript?.Contains(TranscribedFileSearchQuery, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (TranscribedDateFilter.HasValue)
            {
                query = query.Where(t => t.QueuedAt.Date == TranscribedDateFilter.Value.Date || 
                                       (t.CompletedAt.HasValue && t.CompletedAt.Value.Date == TranscribedDateFilter.Value.Date));
            }

            FilteredTranscribedFiles.Clear();
            foreach (var item in query.OrderByDescending(t => t.QueuedAt))
            {
                FilteredTranscribedFiles.Add(item);
            }
        }

        [RelayCommand]
        private async Task TagSelectedFiles(System.Collections.IList? selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0) return;
            SelectedFile = selectedItems.Cast<FileEntry>().FirstOrDefault();
            CurrentViewName = "DETAILS";
        }
        
        [RelayCommand]
        private void OpenInExplorer(FileEntry? file)
        {
            if (file == null) return;
            string path = file.FilePath;
            try
            {
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                }
                else if (Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error opening Explorer: {ex.Message}";

            }

        }

        [RelayCommand]
        private void OpenInVsCode(FileEntry? file)
        {
            if (file == null) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "code.cmd", // Use code.cmd specifically for better CMD resolution
                    Arguments = $"\"{file.FilePath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
                });

            }

            catch
            {
                try
                {
                    // Fallback to "code"
                    Process.Start(new ProcessStartInfo { FileName = "code", Arguments = $"\"{file.FilePath}\"", UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error opening VS Code: {ex.Message}";
                }


            }

        }

        [RelayCommand]
        private void OpenInPowerShell(FileEntry? file)
        {
            if (file == null) return;
            try
            {
                string dir = File.Exists(file.FilePath) ? Path.GetDirectoryName(file.FilePath)! : file.FilePath;
                Process.Start(new ProcessStartInfo("powershell.exe", $"-NoExit -Command \"Set-Location -Path '{dir}'\"") { UseShellExecute = true });

            }

            catch (Exception ex)
            {
                StatusMessage = $"Error opening PowerShell: {ex.Message}";

            }

        }

        [RelayCommand]
        private void OpenInCmd(FileEntry? file)
        {
            if (file == null) return;
            try
            {
                string dir = File.Exists(file.FilePath) ? Path.GetDirectoryName(file.FilePath)! : file.FilePath;
                Process.Start(new ProcessStartInfo("cmd.exe", $"/K \"cd /d \"{dir}\"\"") { UseShellExecute = true });

            }

            catch (Exception ex)
            {
                StatusMessage = $"Error opening CMD: {ex.Message}";

            }

        }

        private bool _isProcessingQueue = false;
        private async Task ProcessTranscriptionQueueAsync()
        {
            if (_isProcessingQueue) return;
            _isProcessingQueue = true;

            try
            {
                while (true)
                {
                    var task = TranscribeQueue.FirstOrDefault(t => t.Status == TranscriptionStatus.Queued);
                    if (task == null) break;

                    task.Status = TranscriptionStatus.Processing;
                    await _dbService.SaveTranscriptionTaskAsync(task); // Update status in DB
                    try
                    {
                        // Capture current model settings
                        string modelLabel = $"Whisper {SelectedWhisperModel}" + (IsEnglishOnly ? " (English)" : " (Multilingual)");
                        task.ModelUsed = modelLabel;
                        
                        task.Transcript = await _aiService.TranscribeAudioAsync(task.FilePath, SelectedWhisperModel, IsEnglishOnly, UseGpu);
                        task.Status = TranscriptionStatus.Completed;
                        task.CompletedAt = DateTime.Now;
                        
                        var file = AllFiles.FirstOrDefault(f => f.FilePath == task.FilePath);
                        if (file != null)
                        {
                            var meta = await _dbService.GetMetadataAsync(file.FileId.ToString()) ?? new FileMetadata { FileId = file.FileId.ToString() };
                            meta.Transcription = task.Transcript;
                            
                            // Generate embedding for semantic search
                            StatusMessage = $"Generating embedding for {task.FileName}...";
                            meta.VectorEmbedding = await _aiService.GenerateEmbeddingAsync(task.Transcript);
                            meta.LastAnalyzed = DateTime.Now;
                            
                            await _dbService.SaveMetadataAsync(meta);
                        }
                    }
                    catch (Exception ex)
                    {
                        task.Status = TranscriptionStatus.Error;
                        task.ErrorMessage = ex.Message;
                    }
                    await _dbService.SaveTranscriptionTaskAsync(task); // Save final status and transcript
                }


            }

            finally
            {
                _isProcessingQueue = false;

            }

        }


        private readonly IMerkleTreeService _merkleService;

        public MainViewModel(IUsnJournalService usnService, IEtwMonitorService etwService, IDatabaseService dbService,
            IMerkleTreeService merkleService,
            IAiService aiService,
            IFileScannerService scannerService,
            IAnalyticsService analyticsService)
        {
            _usnService = usnService;
            _etwService = etwService;
            _dbService = dbService;
            _aiService = aiService;
            _merkleService = merkleService;
            _scannerService = scannerService;
            _analyticsService = analyticsService;

            // Log App Launch
            _analyticsService.LogEventAsync("app_launch", new Dictionary<string, object>
            {
                { "os_version", Environment.OSVersion.ToString() },
                { "app_version", "1.1.0" }
            });

            IsCudaAvailable = _aiService.IsCudaAvailable;

            // Wire up events
            _etwService.OnFileActivity += EtwService_OnFileActivity;

            // Load initial data
            
            // Load Settings and Check GPU
            Task.Run(async () =>
            {
                try
                {
                    // Ensure DB is initialized
                    await _dbService.InitializeAsync("SecurePassword123!");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DB Init Failed in Constructor: {ex.Message}");
                }

                // Check GPU Support
                IsGpuAvailable = GpuHasherService.IsGpuSupported;
                if (!IsGpuAvailable)
                {
                    GpuCheckboxContent = "Use GPU Acceleration (No GPU Detected)";
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => UseGpu = false);
                }
                else
                {
                    GpuCheckboxContent = "Use GPU Acceleration (Experimental)";
                    // Load saved setting
                    string? useGpuVal = await _dbService.GetSettingAsync("UseGpu");
                    if (bool.TryParse(useGpuVal, out bool g))
                    {
                         await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => UseGpu = g);
                    }
                }

                // Load Threads Setting
                string? useThreadsVal = await _dbService.GetSettingAsync("UseThreads");
                 if (bool.TryParse(useThreadsVal, out bool t))
                {
                     await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => UseThreads = t);
                }
                
                // Load Verify Setting
                string? verifyVal = await _dbService.GetSettingAsync("VerifyGpuHash");
                 if (bool.TryParse(verifyVal, out bool v))
                {
                     await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => VerifyGpuHash = v);
                }
            });
            _ = InitializeAsync();

            _eventProcessingTimer = new System.Timers.Timer(500);
            _eventProcessingTimer.Elapsed += (s, e) => ProcessEventQueue();
            _eventProcessingTimer.Start();
        }

        private async Task InitializeAsync()
        {
            await LoadAllFiles();
            await RefreshIgnoreList();
            await LoadTranscriptionHistory();
            await LoadIgnoredProcesses();
        }

        private async Task LoadTranscriptionHistory()
        {
            var tasks = await _dbService.GetAllTranscriptionTasksAsync();
            foreach (var task in tasks)
            {
                if (!TranscribeQueue.Any(t => t.Id == task.Id))
                {
                    TranscribeQueue.Add(task);
                }
            }
        }

        private async Task LoadIgnoredProcesses()
        {
            var processes = await _dbService.GetIgnoredProcessesAsync();
            IgnoredProcesses.Clear();
            foreach (var p in processes)
            {
                IgnoredProcesses.Add(p);
            }
        }
        
        private readonly System.Collections.Generic.Dictionary<string, FileActivityEvent> _activeFileEvents = new();
        
        private void EtwService_OnFileActivity(object? sender, FileActivityEvent e)
        {
            if (IsLiveActivityPaused) return;

            // Log to DB (Fire and forget)
            Task.Run(async () => await _dbService.SaveAuditLogAsync(e));

            // Filtering (Do this early to avoid queue spam)
            if (!ShowDllEvents && e.FilePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return;
            if (!ShowExeEvents && e.FilePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return;
            if (!ShowMuiEvents && e.FilePath.EndsWith(".mui", StringComparison.OrdinalIgnoreCase)) return;
            if (!ShowPsd1Events && e.FilePath.EndsWith(".psd1", StringComparison.OrdinalIgnoreCase)) return;
            if (!ShowPnfEvents && e.FilePath.EndsWith(".pnf", StringComparison.OrdinalIgnoreCase)) return;

            // Directory Filters
            if (!ShowWindowsEvents && e.FilePath.Contains(@"\Windows\", StringComparison.OrdinalIgnoreCase))
            {
                // If the user UNCHECKS "Show Windows", we hide everything in C:\Windows.
                // However, user might have CHECKED "Show System32".
                // If ShowSystem32 is TRUE, we should probably allow it? 
                // BUT "Windows" is the parent. 
                // Let's assume hierarchy: If Windows is hidden, EVERYTHING inside is hidden. 
                // Unless we want complex inclusion logic. 
                // "exclude any files under Windows directory".
                
                // Let's check strict hierarchy.
                return;
            }

            if (!ShowSystem32Events && e.FilePath.Contains(@"\Windows\System32\", StringComparison.OrdinalIgnoreCase))
            {
                // Special case: PowerShell might be inside System32, check user preference order
                // The user asked for two checkboxes. If System32 is unchecked, it hides everything in it.
                // But if they want to hide ONLY PowerShell but SHOW System32, we need to be careful.
                // User Request: "checkbox System32 default Off to exclude files under Windows\System32"
                // User Request: "checkbox PowerShell default Off to exclude Windows\System32\WindowsPowerShell"
                
                // If System32 is hidden, everything there is hidden (including PS).
                // If System32 is SHOWN, we check PowerShell specific filter.
                
                // Wait, if ShowSystem32 is FALSE (default), it hides System32.
                // If ShowPowerShell is FALSE (default), it hides PS.
                
                // Since PS is usually inside System32, if ShowSystem32 is FALSE, PS is already hidden.
                // So checking ShowSystem32 first covers it.
                return;
            }
            
            // If we are here, System32 is SHOWING (or the file is not in System32).
            // Check PowerShell specifically (e.g. if it's elsewhere or if user enabled System32 but disabled PS)
            if (!ShowPowerShellEvents && e.FilePath.Contains(@"WindowsPowerShell", StringComparison.OrdinalIgnoreCase)) return;

            _eventQueue.Enqueue(e);
        }

        private void ProcessEventQueue(object? sender = null, EventArgs? e = null)
        {
            if (_eventQueue.IsEmpty) return;

            // Dequeue all pending items
            var newEvents = new System.Collections.Generic.List<FileActivityEvent>();
            while (_eventQueue.TryDequeue(out var ev))
            {
                newEvents.Add(ev);
            }

            // Deduplicate local batch (keep latest for each file)
            var batchLookup = new System.Collections.Generic.Dictionary<string, FileActivityEvent>();
            foreach(var item in newEvents)
            {
                batchLookup[item.FilePath] = item;
            }

            foreach (var kvp in batchLookup)
            {
                var evt = kvp.Value;
                if (_activeFileEvents.TryGetValue(evt.FilePath, out var existingEvent))
                {
                    // Update existing -> Move to Top
                    existingEvent.Timestamp = evt.Timestamp;
                    existingEvent.Operation = evt.Operation;
                    existingEvent.ProcessName = evt.ProcessName;
                    existingEvent.UserName = evt.UserName;
                    
                    int oldIndex = RecentActivity.IndexOf(existingEvent);
                    if (oldIndex > 0)
                    {
                        RecentActivity.Move(oldIndex, 0);
                    }
                }
                else
                {
                     // New Entry
                    _activeFileEvents[evt.FilePath] = evt;
                    RecentActivity.Insert(0, evt);
                }
            }

            // Enforce Limit after batch update
            while (RecentActivity.Count > MaxLiveItems)
            {
                var last = RecentActivity.Last();
                RecentActivity.RemoveAt(RecentActivity.Count - 1);
                _activeFileEvents.Remove(last.FilePath);
            }
        }

        [RelayCommand]
        public async Task ScanDuplicates()
        {
            try
            {
                StatusMessage = "Initializing database...";
                await _dbService.InitializeAsync("SecurePassword123!");
                
                StatusMessage = "Scanning for duplicates...";
                var dupeList = await _dbService.GetDuplicateFilesAsync();
                
                DuplicateFiles.Clear();
                foreach (var file in dupeList)
                {
                    DuplicateFiles.Add(file);
                }

                StatusMessage = $"Found {DuplicateFiles.Count} duplicate candidates.";

            }

            catch (Exception ex)
            {
                StatusMessage = $"Duplicate Scan Error: {ex.Message}";

            }

        }

        [RelayCommand]
        public async Task Search()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery)) return;

            try
            {
                await _dbService.InitializeAsync("SecurePassword123!");
                
                StatusMessage = "Analyzing query...";
                var embeddingStr = await _aiService.GenerateEmbeddingAsync(SearchQuery);
                
                // Parse embedding result
                if (string.IsNullOrEmpty(embeddingStr) || embeddingStr == "[]" || embeddingStr.StartsWith("Error"))
                {
                    StatusMessage = "Search failed: Please download AI models first.";
                    return;
                }


                var vector = embeddingStr.Split(',').Select(float.Parse).ToArray();
                
                StatusMessage = "Searching database...";
                var results = await _dbService.SearchFilesAsync(vector);

                SearchResults.Clear();
                foreach(var file in results)
                {
                    SearchResults.Add(file);
                }

                StatusMessage = $"Found {SearchResults.Count} relevant files.";

            }

            catch (Exception ex)
            {
                StatusMessage = $"Search Error: {ex.Message}";

            }

        }

        [RelayCommand]
        public async Task StartMonitoring()
        {
            StatusMessage = "Initializing AI Models...";
            await _aiService.DownloadModelsAsync(new Progress<string>(s => StatusMessage = s));
            
            StatusMessage = "Initializing Database...";
            await _dbService.InitializeAsync("SecurePassword123!"); // User should input this

            StatusMessage = "Starting USN Journal Reader for all drives...";
            await _usnService.InitializeAllDrivesAsync();

            // Load historical offline changes from DB first
            StatusMessage = "Loading offline history...";
            await LoadOfflineHistory();
            
            // --- OFFLINE CHANGE DETECTION ---
            StatusMessage = "Checking for offline changes...";
            await Task.Run(() =>
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable));
                foreach (var drive in drives)
                {
                    try
                    {
                        var changes = _usnService.ReadChanges(drive.Name);
                        foreach (var change in changes)
                        {
                            var evt = new FileActivityEvent
                            {
                                Timestamp = change.LastModified,
                                FilePath = string.IsNullOrEmpty(change.FilePath) ? Path.Combine(drive.Name, change.FileName) : change.FilePath,
                                ProcessId = 0,
                                ProcessName = "System (Offline)",
                                UserName = "System",
                                Operation = FileOperation.Write // USN mostly tracks writes/changes
                            };

                             // Dispatch to UI
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                OfflineActivity.Insert(0, evt);
                            });
                            
                            // Also log to DB
                             _dbService.SaveAuditLogAsync(evt).Wait();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error reading USN for {drive.Name}: {ex.Message}");
                    }
                }
            });

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
