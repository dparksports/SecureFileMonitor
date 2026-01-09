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

namespace SecureFileMonitor.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IUsnJournalService _usnService;
        private readonly IEtwMonitorService _etwService;
        private readonly IDatabaseService _dbService;
        private readonly IAiService _aiService;

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
        public ObservableCollection<FileEntry> DuplicateFiles { get; } = new();
        public ObservableCollection<FileEntry> SearchResults { get; } = new();
        public ObservableCollection<FileEntry> AllFiles { get; } = new();
        public ObservableCollection<FileEntry> FilteredFiles { get; } = new();
        public ObservableCollection<TranscriptionTask> TranscribeQueue { get; } = new();

        [ObservableProperty]
        private TranscriptionTask? _selectedTranscriptionTask;

        [ObservableProperty]
        private bool _scanReparseFolders = true;

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

        // Sorting
        [ObservableProperty]
        private string _sortBy = "Name"; // Name, CreationTime, LastModified

        // File Type Filtering
        [ObservableProperty]
        private string _fileTypeFilter = "All"; // All, Audio, Video, Office, PDF, Text, Image

        // Date Filtering
        [ObservableProperty]
        private DateTime? _dateFilterStart;

        [ObservableProperty]
        private DateTime? _dateFilterEnd;

        partial void OnSortByChanged(string value) => ApplyFilters();
        partial void OnFileTypeFilterChanged(string value) => ApplyFilters();
        partial void OnDateFilterStartChanged(DateTime? value) => ApplyFilters();
        partial void OnDateFilterEndChanged(DateTime? value) => ApplyFilters();

        [RelayCommand]
        public void SwitchView(string viewName)
        {
            CurrentViewName = viewName;
        }

        private static readonly Dictionary<string, string[]> FileTypeExtensions = new()
        {
            { "Audio", new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a" } },
            { "Video", new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm" } },
            { "Office", new[] { ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods" } },
            { "PDF", new[] { ".pdf" } },
            { "Text", new[] { ".txt", ".log", ".md", ".csv", ".json", ".xml" } },
            { "Image", new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".webp" } }
        };

        private void ApplyFilters()
        {
            var query = AllFiles.AsEnumerable();

            // Type Filter
            if (FileTypeFilter != "All" && FileTypeExtensions.ContainsKey(FileTypeFilter))
            {
                var extensions = FileTypeExtensions[FileTypeFilter];
                query = query.Where(f => extensions.Contains(f.FileExtension.ToLowerInvariant()));
            }

            // Date Filter
            if (DateFilterStart.HasValue)
            {
                query = query.Where(f => f.CreationTime >= DateFilterStart.Value || f.LastModified >= DateFilterStart.Value);
            }
            if (DateFilterEnd.HasValue)
            {
                query = query.Where(f => f.CreationTime <= DateFilterEnd.Value || f.LastModified <= DateFilterEnd.Value);
            }

            // Sorting
            query = SortBy switch
            {
                "CreationTime" => query.OrderByDescending(f => f.CreationTime),
                "LastModified" => query.OrderByDescending(f => f.LastModified),
                _ => query.OrderBy(f => f.FileName)
            };

            FilteredFiles.Clear();
            foreach (var file in query.Take(1000)) // Limit for UI performance
            {
                FilteredFiles.Add(file);
            }
        }

        [RelayCommand]
        public async Task LoadAllFiles()
        {
            try
            {
                await _dbService.InitializeAsync("SecurePassword123!");
                
                StatusMessage = "Loading all files from database...";
                var files = await _dbService.GetAllFilesAsync();
                AllFiles.Clear();
                foreach (var f in files)
                {
                    AllFiles.Add(f);
                }
                ApplyFilters();
                
                // Calculate statistics
                TotalFiles = AllFiles.Count;
                TotalVideoFiles = AllFiles.Count(f => FileTypeExtensions["Video"].Contains(f.FileExtension.ToLowerInvariant()));
                TotalAudioFiles = AllFiles.Count(f => FileTypeExtensions["Audio"].Contains(f.FileExtension.ToLowerInvariant()));
                TotalCodeFiles = AllFiles.Count(f => new[] { ".cs", ".py", ".js", ".ts", ".java", ".cpp", ".c", ".h", ".go", ".rs" }.Contains(f.FileExtension.ToLowerInvariant()));
                LastScannedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                
                StatusMessage = $"Loaded {AllFiles.Count} files.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load Error: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task DownloadModels()
        {
            await _aiService.DownloadModelsAsync(new Progress<string>(s => StatusMessage = s));
        }

        private readonly IFileScannerService _scannerService;
        
        [ObservableProperty]
        private FileEntry? _selectedFile;

        [ObservableProperty]
        private string _selectedFileTags = string.Empty;

        partial void OnSelectedFileChanged(FileEntry? value)
        {
            if (value != null)
            {
                // Ideally load metadata from DB
                // For now, clear tags or load if we had them in FileEntry
                // We added Tags to FileMetadata, not FileEntry. Need DB lookup.
                // Async void is tricky here, better to use a Command or specialized loader.
                // Stub:
                SelectedFileTags = "Loading...";
                Task.Run(async () => 
                {
                    var meta = await _dbService.GetMetadataAsync(value.FileId.ToString()); // Verify ID mapping
                    App.Current.Dispatcher.Invoke(() => 
                    {
                        SelectedFileTags = meta?.Tags ?? ""; 
                    });
                });
            }
        }

        [RelayCommand]
        public async Task SaveTags()
        {
            if (SelectedFile == null) return;
            
            var meta = await _dbService.GetMetadataAsync(SelectedFile.FileId.ToString()) ?? new FileMetadata { FileId = SelectedFile.FileId.ToString() };
            meta.Tags = SelectedFileTags;
            await _dbService.SaveMetadataAsync(meta);
            StatusMessage = "Tags saved.";
        }

        [RelayCommand]
        public async Task ScanDrive()
        {
            try
            {
                StatusMessage = "Initializing database...";
                await _dbService.InitializeAsync("SecurePassword123!");
                
                StatusMessage = "Scanning C: drive for existing files...";
                await _scannerService.ScanDriveAsync("C:\\", ScanReparseFolders, new Progress<string>(s => StatusMessage = s), CancellationToken.None);
                StatusMessage = "Scan Complete. Click 'Load Files' to view.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Scan Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task TranscribeSelectedFiles(System.Collections.IList? selectedItems)
        {
            if (selectedItems == null) return;
            var files = selectedItems.Cast<FileEntry>().ToList();
            
            foreach (var file in files)
            {
                string ext = file.FileExtension.ToLowerInvariant();
                if (FileTypeExtensions["Audio"].Contains(ext) || FileTypeExtensions["Video"].Contains(ext))
                {
                    if (!TranscribeQueue.Any(t => t.FilePath == file.FilePath && t.Status != TranscriptionStatus.Completed))
                    {
                        TranscribeQueue.Add(new TranscriptionTask { FilePath = file.FilePath, FileName = file.FileName });
                    }
                }
            }

            _ = ProcessTranscriptionQueueAsync();
            CurrentViewName = "TRANSCRIBE";
        }

        [RelayCommand]
        private async Task TagSelectedFiles(System.Collections.IList? selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0) return;
            SelectedFile = selectedItems.Cast<FileEntry>().FirstOrDefault();
            CurrentViewName = "DETAILS";
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
                    try
                    {
                        task.Transcript = await _aiService.TranscribeAudioAsync(task.FilePath);
                        task.Status = TranscriptionStatus.Completed;
                        task.CompletedAt = DateTime.Now;
                        
                        var file = AllFiles.FirstOrDefault(f => f.FilePath == task.FilePath);
                        if (file != null)
                        {
                            var meta = await _dbService.GetMetadataAsync(file.FileId.ToString()) ?? new FileMetadata { FileId = file.FileId.ToString() };
                            meta.Transcription = task.Transcript;
                            await _dbService.SaveMetadataAsync(meta);
                        }
                    }
                    catch (Exception ex)
                    {
                        task.Status = TranscriptionStatus.Error;
                        task.ErrorMessage = ex.Message;
                    }
                }
            }
            finally
            {
                _isProcessingQueue = false;
            }
        }

        public MainViewModel(IUsnJournalService usnService, IEtwMonitorService etwService, IDatabaseService dbService, IAiService aiService, IFileScannerService scannerService)
        {
            _usnService = usnService;
            _etwService = etwService;
            _dbService = dbService;
            _aiService = aiService;
            _scannerService = scannerService;

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
