using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SecureFileMonitor.Core.Models;
using Microsoft.Extensions.Logging;

namespace SecureFileMonitor.Core.Services
{
    public class FileScannerService : IFileScannerService
    {
        private readonly IDatabaseService _dbService;
        private readonly ILogger<FileScannerService> _logger;

        public FileScannerService(IDatabaseService dbService, ILogger<FileScannerService> logger)
        {
            _dbService = dbService;
            _logger = logger;
        }

        public async Task ScanDriveAsync(string driveLetter, bool scanReparseFolders, IProgress<string> progress, CancellationToken cancellationToken)
        {
            var visitedPaths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await Task.Run(async () =>
            {
                var root = new DirectoryInfo(driveLetter);
                await ScanDirectoryRecursive(root, scanReparseFolders, visitedPaths, progress, cancellationToken);
            });
        }

        private async Task ScanDirectoryRecursive(DirectoryInfo directory, bool scanReparseFolders, System.Collections.Generic.HashSet<string> visitedPaths, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return;

            string canonicalPath = directory.FullName.ToLowerInvariant();
            if (visitedPaths.Contains(canonicalPath)) 
            {
                _logger.LogInformation($"Skipping duplicate or cyclic path: {directory.FullName}");
                return;
            }
            visitedPaths.Add(canonicalPath);

            try
            {
                progress?.Report($"Scanning: {directory.FullName}");

                // Process files
                foreach (var file in directory.EnumerateFiles())
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    try
                    {
                        // Create basic FileEntry
                        // Note: For existing files, we might not have the exact "FRN" easily without P/Invoke or USN query.
                        // We will rely on Path as the unique identifier for this scan, OR we map to existing USN entries if possible.
                        // Simple approach: Upsert based on Path. (Requires DB logic update or careful handling)
                        // If we want to link to USN Journal, we should ideally query the USN record for this file.
                        // For now, let's just create the entry.
                        
                        var entry = new FileEntry
                        {
                            FilePath = file.FullName,
                            FileName = file.Name,
                            FileExtension = file.Extension,
                            FileSize = file.Length,
                            CreationTime = file.CreationTimeUtc,
                            LastModified = file.LastWriteTimeUtc,
                            FileId = 0 // We don't have the FileReferenceNumber easily here without native calls
                        };

                        // Optional: Calculate Hash? (Expensive for all files, maybe just QuickHash or size)
                        entry.CurrentHash = ""; // Deferred hashing
                        
                        await _dbService.SaveFileEntryAsync(entry);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to process file {file.FullName}: {ex.Message}");
                    }
                }

                // Recurse
                foreach (var subDir in directory.EnumerateDirectories())
                {
                    // Skip reparse points ONLY if scanReparseFolders is false.
                    // If true, we rely on visitedPaths loop detection.
                    if (!scanReparseFolders && (subDir.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    
                    await ScanDirectoryRecursive(subDir, scanReparseFolders, visitedPaths, progress, cancellationToken);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip restricted directories
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error scanning directory {directory.FullName}: {ex.Message}");
            }
        }
    }
}
