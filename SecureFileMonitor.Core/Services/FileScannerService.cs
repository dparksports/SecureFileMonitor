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
            var ignoreRules = (await _dbService.GetAllIgnoreRulesAsync()).ToList();

            await Task.Run(async () =>
            {
                var root = new DirectoryInfo(driveLetter);
                await ScanDirectoryRecursive(root, scanReparseFolders, visitedPaths, ignoreRules, progress, cancellationToken);
            });
        }

        private async Task ScanDirectoryRecursive(DirectoryInfo directory, bool scanReparseFolders, System.Collections.Generic.HashSet<string> visitedPaths, System.Collections.Generic.List<IgnoreRule> ignoreRules, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return;

            // Check if directory is ignored
            if (ignoreRules.Any(r => directory.FullName.StartsWith(r.Path, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInformation($"Skipping ignored directory: {directory.FullName}");
                return;
            }

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
                            FileId = 0 
                        };

                        entry.CurrentHash = ""; 
                        
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
                    
                    await ScanDirectoryRecursive(subDir, scanReparseFolders, visitedPaths, ignoreRules, progress, cancellationToken);
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
