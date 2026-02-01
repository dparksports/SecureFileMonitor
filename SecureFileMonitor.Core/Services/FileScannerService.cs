using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SecureFileMonitor.Core.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;

namespace SecureFileMonitor.Core.Services
{
    public class FileScannerService : IFileScannerService
    {
        private readonly IDatabaseService _dbService;
        private readonly IMerkleTreeService _merkleService;
        private readonly IAnalyticsService _analyticsService;
        private readonly ILogger<FileScannerService> _logger;
        private readonly HasherFactory _hasherFactory; // Injected Factory state
        
        // Large file hash queue (thread-safe)
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _largeFileHashQueue = new();
        
        // Write Buffer
        private List<FileEntry> _fileWriteBuffer = new();
        private int _batchSize = 10000; // Default

        
        // Pause Control
        private bool _isPaused = false;
        private int _scannedFilesCount = 0; // Tally for progress reporting
        
        public event EventHandler<(int Remaining, int Total, TimeSpan? ETA)>? OnHashProgressChanged;

        public FileScannerService(
            IDatabaseService dbService,
            IMerkleTreeService merkleService, 
            IAnalyticsService analyticsService,
            ILogger<FileScannerService> logger,
            HasherFactory hasherFactory)
        {
            _dbService = dbService;
            _merkleService = merkleService;
            _analyticsService = analyticsService;
            _logger = logger;
            _hasherFactory = hasherFactory;
        }
        
        public int GetPendingHashCount() => _largeFileHashQueue.Count;

        public void SetPaused(bool paused)
        {
             _isPaused = paused;
        }

        private class ScanState
        {
            public long TotalBytes;
            public DateTime StartTime;
            public bool IsPaused => false; // handled externally
        }

        public async Task ScanDriveAsync(string driveLetter, bool scanReparseFolders, bool skipWindows, bool skipProgramFiles, bool skipRecycleBin, bool forceFullScan, IProgress<(string Status, double? Percent, TimeSpan? ETA, int? ScannedCount)> progress, CancellationToken cancellationToken)
        {
            var ignoreRules = (await _dbService.GetAllIgnoreRulesAsync()).ToList();
            
            // 1. Get Settings for Hasher Logic
            string? useGpuVal = await _dbService.GetSettingAsync("UseGpu");
            string? useThreadsVal = await _dbService.GetSettingAsync("UseThreads");

            string? verifyVal = await _dbService.GetSettingAsync("VerifyGpuHash");
            string? batchSizeVal = await _dbService.GetSettingAsync("ScanBatchSize");
            string? dirBatchSizeVal = await _dbService.GetSettingAsync("DirectoryBatchSize");

            if (int.TryParse(batchSizeVal, out int bSize) && bSize > 0) _batchSize = bSize;
            else _batchSize = 10000;

            
            bool useGpu = bool.TryParse(useGpuVal, out bool g) && g;
            bool useThreads = bool.TryParse(useThreadsVal, out bool t) && t;
            bool verify = bool.TryParse(verifyVal, out bool v) && v;
            
            // Create selected hasher
            var hasher = _hasherFactory.Create(useGpu, useThreads, verify);

            var state = new ScanState 
            { 
                StartTime = DateTime.Now 
            };

            // Estimate Total Bytes (Roughly)
            try
            {
                var driveInfo = new DriveInfo(driveLetter);
                if (driveInfo.IsReady)
                {
                    state.TotalBytes = driveInfo.TotalSize - driveInfo.TotalFreeSpace;
                }
            }
            catch { /* Ignore */ }

            // 2. Initialize Scan Queue (Iterative/Persistent)
            // If forcing full scan, we want to ensure we start fresh.
            if (forceFullScan)
            {
                await _dbService.ClearScanQueueAsync();
            }

            // Add root if queue is empty for THIS DRIVE
            if (await _dbService.GetPendingScanCountForDriveAsync(driveLetter) == 0)
            {
                await _dbService.AddDirectoryToScanQueueAsync(driveLetter, driveLetter);
            }

            // 3. Process Queue in Batches
            int directoryBatchSize = 100; // Default
            if (int.TryParse(dirBatchSizeVal, out int dBatch) && dBatch > 0) directoryBatchSize = dBatch;

            while (!_isPaused && !cancellationToken.IsCancellationRequested)
            {
                // Fetch a batch of directories to process
                var dirBatch = (await _dbService.GetNextDirectoryBatchToScanAsync(directoryBatchSize)).ToList();
                if (dirBatch.Count == 0) break; 

                var processedIds = new List<int>();
                var newSubDirectories = new List<string>();

                foreach (var dirState in dirBatch)
                {
                    if (cancellationToken.IsCancellationRequested || _isPaused) break;

                    // Filter by drive check (lightweight)
                    if (!dirState.Path.StartsWith(driveLetter, StringComparison.OrdinalIgnoreCase))
                    {
                         // Item belongs to another drive session. Skip processing but DO NOT mark as processed.
                         // This allows subsequent scans for other drives to pick it up.
                         continue;
                    }

                    // --- ROBUST SKIP LOGIC FOR QUEUED ITEMS ---
                    if (skipWindows || skipProgramFiles || skipRecycleBin)
                    {
                        bool shouldSkip = false;
                        var segments = dirState.Path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        
                        if (skipWindows && segments.Any(s => s.Equals("Windows", StringComparison.OrdinalIgnoreCase)))
                            shouldSkip = true;
                            
                        if (!shouldSkip && skipProgramFiles && segments.Any(s => s.Equals("Program Files", StringComparison.OrdinalIgnoreCase) || s.Equals("Program Files (x86)", StringComparison.OrdinalIgnoreCase)))
                            shouldSkip = true;

                        if (!shouldSkip && skipRecycleBin && segments.Any(s => s.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)))
                            shouldSkip = true;

                        if (shouldSkip)
                        {
                             processedIds.Add(dirState.Id);
                             continue;
                        }
                    }
                    // ------------------------------------------

                    try
                    {
                        var dirInfo = new DirectoryInfo(dirState.Path);
                        var subDirs = await ScanSingleDirectoryAsync(dirInfo, hasher, scanReparseFolders, skipWindows, skipProgramFiles, skipRecycleBin, forceFullScan, ignoreRules, progress, cancellationToken, state);
                        newSubDirectories.AddRange(subDirs);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error scanning {dirState.Path}: {ex.Message}");
                    }
                    finally
                    {
                        processedIds.Add(dirState.Id);
                    }
                }

                // Batch DB Updates
                if (newSubDirectories.Count > 0)
                {
                    await _dbService.AddDirectoryToScanQueueBatchAsync(newSubDirectories, driveLetter);
                }

                if (processedIds.Count > 0)
                {
                    await _dbService.MarkDirectoriesAsProcessedBatchAsync(processedIds);
                }
            }
            
            // Final Flush
            await FlushBufferAsync();

        }

        private async Task<List<string>> ScanSingleDirectoryAsync(DirectoryInfo directory, IHasherService hasher, bool scanReparseFolders, bool skipWindows, bool skipProgramFiles, bool skipRecycleBin, bool forceFullScan,
            List<IgnoreRule> ignoreRules, 
            IProgress<(string Status, double? Percent, TimeSpan? ETA, int? ScannedCount)>? progress, 
            CancellationToken cancellationToken,
            ScanState state)
        {
            var foundSubDirs = new List<string>();

            // Report Progress (Clean path only, count passed separately)
            progress?.Report((directory.FullName, null, null, _scannedFilesCount));

            var enumOptions = new EnumerationOptions { AttributesToSkip = 0, RecurseSubdirectories = false, IgnoreInaccessible = true };

            try
            {
                 // 1. Queue Subdirectories (Breadth-First Expansion)
                 foreach (var subDir in directory.EnumerateDirectories("*", enumOptions))
                 {
                     if (cancellationToken.IsCancellationRequested) return foundSubDirs;

                      // --- SKIP LOGIC ---
                      if (skipWindows && subDir.Name.Equals("Windows", StringComparison.OrdinalIgnoreCase)) continue;
                      if (skipProgramFiles && (subDir.Name.Equals("Program Files", StringComparison.OrdinalIgnoreCase) || subDir.Name.Equals("Program Files (x86)", StringComparison.OrdinalIgnoreCase))) continue;
                      if (skipRecycleBin && subDir.Name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)) continue;
                      // ------------------

                     bool isReparse = (subDir.Attributes & FileAttributes.ReparsePoint) != 0;
                     if (isReparse && !scanReparseFolders) continue;

                     bool isIgnoredDir = ignoreRules.Any(r => subDir.FullName.StartsWith(r.Path, StringComparison.OrdinalIgnoreCase));
                     if (isIgnoredDir) continue;
                     
                     foundSubDirs.Add(subDir.FullName);
                 }

                 // 2. Process Files
                 System.Collections.Generic.List<string> filePaths = new();
                 foreach (var file in directory.EnumerateFiles("*", enumOptions))
                 {
                     if (cancellationToken.IsCancellationRequested) return foundSubDirs;
                     filePaths.Add(file.FullName);
                 }

                 foreach (var filePath in filePaths)
                 {
                     _scannedFilesCount++; // Increment tally
                     
                     var file = new FileInfo(filePath);
                     var existingEntry = await _dbService.GetFileEntryAsync(filePath);
                     bool needsHashing = true;
                     
                     if (existingEntry != null && !forceFullScan)
                     {
                         if (existingEntry.LastModified == file.LastWriteTimeUtc && existingEntry.FileSize == file.Length)
                             needsHashing = false;
                     }

                     string newHash = existingEntry?.CurrentHash ?? "";

                     if (needsHashing)
                     {
                         // Optional: detailed file progress (might be too spammy, but useful for debugging hang)
                         if (file.Length > 10 * 1024 * 1024) // Only for files > 10MB
                            progress?.Report(($"Hashing {file.Name}...", null, null, _scannedFilesCount));

                         if (file.Length > 50 * 1024 * 1024)
                         {
                             _largeFileHashQueue.Enqueue(filePath);
                             newHash = "PENDING";
                         }
                         else
                         {
                             newHash = await hasher.ComputeHashAsync(filePath);
                         }
                         
                         var entry = new FileEntry 
                         { 
                             FilePath = filePath, 
                             FileName = file.Name, 
                             FileSize = file.Length, 
                             LastModified = file.LastWriteTimeUtc,
                             CurrentHash = newHash 
                         };
                         
                         _fileWriteBuffer.Add(entry);
                         if (_fileWriteBuffer.Count >= _batchSize)
                         {
                             await FlushBufferAsync();
                         }
                     }
                 }
            }
            catch (Exception ex) 
            {
                _logger.LogError($"Failed dir scan {directory.FullName}: {ex.Message}");
                throw; // Rethrow to be caught by batch loop for logging context
            }
            
            return foundSubDirs;
        }

        // Keep Background Hashing Logic same...
        public async Task StartBackgroundHashingAsync(IProgress<(string Status, int Remaining, int Total, TimeSpan? ETA)> progress, CancellationToken cancellationToken)
        {
             // Use Factory to support settings even for background hashing
             string? useGpuVal = await _dbService.GetSettingAsync("UseGpu");
             string? useThreadsVal = await _dbService.GetSettingAsync("UseThreads");
             string? verifyVal = await _dbService.GetSettingAsync("VerifyGpuHash");
             
             bool useGpu = bool.TryParse(useGpuVal, out bool g) && g;
             bool useThreads = bool.TryParse(useThreadsVal, out bool t) && t;
             bool verify = bool.TryParse(verifyVal, out bool v) && v;
             
             var hasher = _hasherFactory.Create(useGpu, useThreads, verify);

            int total = _largeFileHashQueue.Count;
            if (total == 0) return;
            
            int processed = 0;
            long totalBytesProcessed = 0;
            DateTime startTime = DateTime.Now;
            
            while (_largeFileHashQueue.TryDequeue(out string? filePath) && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (!fileInfo.Exists) continue;
                    
                    progress?.Report(($"Hashing: {fileInfo.Name}", _largeFileHashQueue.Count, total, null));
                    
                    string hashString = await hasher.ComputeHashAsync(filePath);
                    
                    // Update DB
                    var entry = await _dbService.GetFileEntryAsync(filePath);
                    if (entry != null)
                    {
                        entry.CurrentHash = hashString;
                        await _dbService.SaveFileEntryAsync(entry);
                    }
                    
                    processed++;
                    totalBytesProcessed += fileInfo.Length;
                    
                    // Calculate ETA
                    TimeSpan elapsed = DateTime.Now - startTime;
                    TimeSpan? eta = null;
                    if (elapsed.TotalSeconds > 1 && processed > 0)
                    {
                        // Safely calculate rate to avoid div by zero
                        double rate = totalBytesProcessed / elapsed.TotalSeconds;
                        // Estimate remaining bytes (rough: use average file size of processed files)
                        long avgBytes = totalBytesProcessed / processed;
                        int remaining = _largeFileHashQueue.Count;
                        if (rate > 0)
                        {
                            eta = TimeSpan.FromSeconds((remaining * avgBytes) / rate);
                        }
                    }
                    
                    OnHashProgressChanged?.Invoke(this, (_largeFileHashQueue.Count, total, eta));
                    progress?.Report(($"Hashed: {fileInfo.Name}", _largeFileHashQueue.Count, total, eta));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to hash large file {filePath}: {ex.Message}");
                }
            }
            
            progress?.Report(("Background hashing complete.", 0, total, null));
        }

        private async Task FlushBufferAsync()
        {
            if (_fileWriteBuffer.Count > 0)
            {
                // Create copy to pass to DB to avoid modification during await if we were parallel (we aren't here but safety first)
                var batch = _fileWriteBuffer.ToList();
                _fileWriteBuffer.Clear();
                await _dbService.SaveFileEntriesBatchAsync(batch);
            }
        }

        private async Task CheckForDeletedFilesAsync(string driveLetter, System.Collections.Generic.HashSet<string> foundFiles, System.Collections.Generic.List<IgnoreRule> ignoreRules)
        {
            try 
            {
                // Fetch all DB entries for this drive (naive substring match for now, ideally DB query filters)
                var allEntries = await _dbService.GetAllFilesAsync();
                var driveEntries = allEntries.Where(e => e.FilePath.StartsWith(driveLetter, StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var entry in driveEntries)
                {
                    if (!foundFiles.Contains(entry.FilePath))
                    {
                        // File is in DB but not found on disk -> DELETED
                        
                        // Check ignore rules (logging only)
                        bool isIgnored = ignoreRules.Any(r => entry.FilePath.StartsWith(r.Path, StringComparison.OrdinalIgnoreCase));

                        if (!isIgnored)
                        {
                            var evt = new FileActivityEvent
                            {
                                Timestamp = DateTime.Now,
                                Operation = FileOperation.Delete,
                                FilePath = entry.FilePath,
                                ProcessName = "Scanner (Offline Detection)",
                                ProcessId = 0,
                                UserName = "System"
                            };
                            await _dbService.SaveAuditLogAsync(evt);
                        }

                        // Remove from DB
                        await _dbService.DeleteFileEntryAsync(entry.FilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                 _logger.LogWarning($"Error checking deleted files: {ex.Message}");
            }
        }
    }
}
