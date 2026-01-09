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
        private readonly ILogger<FileScannerService> _logger;
        
        // Large file hash queue (thread-safe)
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _largeFileHashQueue = new();
        private int _totalLargeFilesQueued = 0;
        
        public event EventHandler<(int Remaining, int Total, TimeSpan? ETA)>? OnHashProgressChanged;

        public FileScannerService(IDatabaseService dbService, ILogger<FileScannerService> logger)
        {
            _dbService = dbService;
            _logger = logger;
        }
        
        public int GetPendingHashCount() => _largeFileHashQueue.Count;

        private class ScanState
        {
            public long ProcessedBytes;
            public long TotalBytes;
            public DateTime StartTime;
        }

        public async Task ScanDriveAsync(string driveLetter, bool scanReparseFolders, IProgress<(string Status, double? Percent, TimeSpan? ETA)> progress, CancellationToken cancellationToken)
        {
            var visitedPaths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ignoreRules = (await _dbService.GetAllIgnoreRulesAsync()).ToList();

            var state = new ScanState 
            { 
                StartTime = DateTime.Now 
            };

            try
            {
                var driveInfo = new DriveInfo(driveLetter);
                if (driveInfo.IsReady)
                {
                    state.TotalBytes = driveInfo.TotalSize - driveInfo.TotalFreeSpace;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not get drive info for {driveLetter}: {ex.Message}");
            }

            await Task.Run(async () =>
            {
                var root = new DirectoryInfo(driveLetter);
                await ScanDirectoryRecursive(root, scanReparseFolders, visitedPaths, ignoreRules, progress, cancellationToken, state);
            });
        }

        private async Task ScanDirectoryRecursive(DirectoryInfo directory, bool scanReparseFolders, 
            System.Collections.Generic.HashSet<string> visitedPaths, 
            System.Collections.Generic.List<IgnoreRule> ignoreRules, 
            IProgress<(string Status, double? Percent, TimeSpan? ETA)>? progress, 
            CancellationToken cancellationToken,
            ScanState state)
        {
            if (cancellationToken.IsCancellationRequested) return;

            // Check if directory is ignored
            if (ignoreRules.Any(r => directory.FullName.StartsWith(r.Path, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            string canonicalPath = directory.FullName.ToLowerInvariant();
            if (visitedPaths.Contains(canonicalPath)) return;
            visitedPaths.Add(canonicalPath);

            try
            {
                // Report Progress
                if (state.TotalBytes > 0)
                {
                    long current = Interlocked.Read(ref state.ProcessedBytes);
                    double percent = (double)current / state.TotalBytes * 100.0;
                    TimeSpan elapsed = DateTime.Now - state.StartTime;
                    TimeSpan? eta = null;
                    if (percent > 0.1) 
                    {
                        double rate = current / elapsed.TotalSeconds; // bytes per second
                        if (rate > 0)
                        {
                            double remainingBytes = state.TotalBytes - current;
                            eta = TimeSpan.FromSeconds(remainingBytes / rate);
                        }
                    }
                    progress?.Report(($"Scanning: {directory.FullName}", percent, eta));
                }
                else
                {
                    progress?.Report(($"Scanning: {directory.FullName}", null, null));
                }

                var enumOptions = new EnumerationOptions { AttributesToSkip = 0, RecurseSubdirectories = false, IgnoreInaccessible = true };

                // Process files
                foreach (var file in directory.EnumerateFiles("*", enumOptions))
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    // Update processed bytes
                    Interlocked.Add(ref state.ProcessedBytes, file.Length);

                    // Skip ignored files
                    if (ignoreRules.Any(r => file.FullName.Equals(r.Path, StringComparison.OrdinalIgnoreCase))) continue;

                    try
                    {
                        var entry = new FileEntry
                        {
                            FilePath = file.FullName,
                            FileName = file.Name,
                            FileExtension = file.Extension,
                            FileSize = file.Length,
                            CreationTime = file.CreationTimeUtc,
                            LastModified = file.LastWriteTimeUtc,
                            LastAccessTime = file.LastAccessTimeUtc,
                            FileId = 0 
                        };

                        entry.CurrentHash = await CalculateHashAsync(file.FullName, cancellationToken);
                        await _dbService.SaveFileEntryAsync(entry);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to process file {file.FullName}: {ex.Message}");
                    }
                }

                // Recurse
                foreach (var subDir in directory.EnumerateDirectories("*", enumOptions))
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    bool isReparse = (subDir.Attributes & FileAttributes.ReparsePoint) != 0;
                    
                    if (isReparse && !scanReparseFolders)
                    {
                        continue;
                    }
                    
                    await ScanDirectoryRecursive(subDir, scanReparseFolders, visitedPaths, ignoreRules, progress, cancellationToken, state);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error scanning directory: {directory.FullName}");
            }
        }

        private async Task<string> CalculateHashAsync(string filePath, CancellationToken cancellationToken)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 50 * 1024 * 1024) 
                {
                    // Queue for background hashing instead of skipping
                    _largeFileHashQueue.Enqueue(filePath);
                    Interlocked.Increment(ref _totalLargeFilesQueued);
                    return "PENDING"; // Mark as pending
                }

                using var md5 = MD5.Create();
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] hash = await md5.ComputeHashAsync(stream, cancellationToken);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch
            {
                return "";
            }
        }
        
        public async Task StartBackgroundHashingAsync(IProgress<(string Status, int Remaining, int Total, TimeSpan? ETA)> progress, CancellationToken cancellationToken)
        {
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
                    
                    // Use SHA256 for large files (stronger hash)
                    using var sha256 = SHA256.Create();
                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    byte[] hash = await sha256.ComputeHashAsync(stream, cancellationToken);
                    string hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    
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
                        double rate = totalBytesProcessed / elapsed.TotalSeconds;
                        // Estimate remaining bytes (rough: use average file size)
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
            
            // Reset counter for next scan
            Interlocked.Exchange(ref _totalLargeFilesQueued, 0);
            progress?.Report(("Background hashing complete.", 0, total, null));
        }
    }
}
