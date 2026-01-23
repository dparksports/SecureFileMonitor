using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using SecureFileMonitor.Core.Models;

namespace SecureFileMonitor.Core.Services
{
    public class DatabaseService : IDatabaseService
    {
        private SQLiteAsyncConnection? _connection;
        private readonly string _dbPath;
        private readonly int _offlineEventLimit = 1000; // Safeguard limit for queries

        public DatabaseService()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecureFileMonitor");
            Directory.CreateDirectory(folder);
            _dbPath = Path.Combine(folder, "secure_monitor.db");
        }

        public async Task InitializeAsync(string password)
        {
            var options = new SQLiteConnectionString(_dbPath, true, key: password);
            _connection = new SQLiteAsyncConnection(options);

            await _connection!.CreateTableAsync<FileEntry>();
            await _connection!.CreateTableAsync<FileActivityEvent>();
            await _connection!.CreateTableAsync<FileMetadata>();
            await _connection!.CreateTableAsync<IgnoreRule>();
            await _connection!.CreateTableAsync<TranscriptionTask>();
            await _connection!.CreateTableAsync<IgnoredProcess>();
            await _connection!.CreateTableAsync<IgnoredProcess>();
            await _connection!.CreateTableAsync<FileMerkleTree>();
            await _connection!.CreateTableAsync<AppSetting>();
            await _connection!.CreateTableAsync<DirectoryScanState>();
        }

        public async Task SaveFileEntryAsync(FileEntry entry)
        {
            // Upsert
            var existing = await _connection!.Table<FileEntry>().Where(x => x.FilePath == entry.FilePath).FirstOrDefaultAsync();
            if (existing != null)
            {
                entry.FileId = existing.FileId; // Keep ID? Wait, FileEntry has FRN as ID.
                // Actually, FileEntry key should be FilePath for lookup, but FRN for filesystem identity.
                // SQLite needs a Primary Key. Let's assume FileEntry defines one or we add [PrimaryKey] to Model.
                await _connection!.UpdateAsync(entry);
            }
            else
            {
                await _connection!.InsertAsync(entry);
            }
        }

        public async Task DeleteFileEntryAsync(string filePath)
        {
             // Assumes FilePath is primary key or unique index
             // Since FileEntry might not have [PrimaryKey] on FilePath (FileId is FRN?), we use explicit delete by query
             var entry = await GetFileEntryAsync(filePath);
             if (entry != null)
             {
                 await _connection!.DeleteAsync(entry);
             }
        }



        // Transcription History
        public async Task SaveTranscriptionTaskAsync(TranscriptionTask task)
        {
            await _connection!.InsertOrReplaceAsync(task);
        }

        public async Task<IEnumerable<TranscriptionTask>> GetAllTranscriptionTasksAsync()
        {
            return await _connection!.Table<TranscriptionTask>().ToListAsync();
        }

        // Ignored Processes
        public async Task AddIgnoredProcessAsync(string processName)
        {
            await _connection!.InsertOrReplaceAsync(new IgnoredProcess { ProcessName = processName });
        }

        public async Task RemoveIgnoredProcessAsync(string processName)
        {
            await _connection!.DeleteAsync<IgnoredProcess>(processName);
        }

        public async Task<IEnumerable<string>> GetIgnoredProcessesAsync()
        {
            var list = await _connection!.Table<IgnoredProcess>().ToListAsync();
            return list.Select(p => p.ProcessName);
        }

        public async Task<FileEntry> GetFileEntryAsync(string filePath)
        {
            return await _connection!.Table<FileEntry>().Where(x => x.FilePath == filePath).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<FileEntry>> GetAllEntriesAsync()
        {
            return await _connection!.Table<FileEntry>().ToListAsync();
        }

        public async Task<IEnumerable<FileEntry>> GetDuplicateFilesAsync()
        {
            // Naive approach since sqlite-net doesn't support easy GROUP BY HAVING via LINQ
            var all = await _connection!.Table<FileEntry>().ToListAsync();
            var duplicates = all.GroupBy(x => x.CurrentHash)
                                .Where(g => g.Count() > 1 && !string.IsNullOrEmpty(g.Key))
                                .SelectMany(g => g)
                                .ToList();
            return duplicates;
        }

        public async Task SaveAuditLogAsync(FileActivityEvent activity)
        {
            await _connection!.InsertAsync(activity);
        }

        public async Task<List<FileActivityEvent>> GetRecentOfflineActivityAsync(int limit)
        {
            // Cap limit to safeguard
            int safeLimit = Math.Min(limit, _offlineEventLimit);

            // Fetch events where ProcessName indicates offline changes (System (Offline) or Scanner (Offline Detection))
            // Order by Timestamp Descending
            return await _connection!.Table<FileActivityEvent>()
                                     .Where(e => e.ProcessName == "System (Offline)" || e.ProcessName == "Scanner (Offline Detection)")
                                     .OrderByDescending(e => e.Timestamp)
                                     .Take(safeLimit)
                                     .ToListAsync();
        }

        public async Task SaveMetadataAsync(FileMetadata metadata)
        {
            await _connection!.InsertOrReplaceAsync(metadata);
        }

        public async Task<FileMetadata> GetMetadataAsync(string fileId)
        {
            return await _connection!.Table<FileMetadata>().Where(m => m.FileId == fileId).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<FileEntry>> SearchFilesAsync(float[] queryVector, int limit = 10)
        {
            // 1. Fetch all metadata (Naive approach for small datasets)
            // For production with thousands of files, use a vector DB or specialized index.
            var allMetadata = await _connection!.Table<FileMetadata>().ToListAsync();
            
            var results = new List<(string FileId, double Score)>();

            foreach (var meta in allMetadata)
            {
                if (string.IsNullOrEmpty(meta.VectorEmbedding)) continue;

                try
                {
                    var fileVector = meta.VectorEmbedding.Split(',').Select(float.Parse).ToArray();
                    var score = CosineSimilarity(queryVector, fileVector);
                    if (score > 0.3) // Threshold
                    {
                        results.Add((meta.FileId, score));
                    }
                }
                catch { /* Ignore parse errors */ }
            }

            var topFileIds = results.OrderByDescending(x => x.Score).Take(limit).Select(x => x.FileId).ToList();

            // 2. Fetch FileEntries
            var entries = new List<FileEntry>();
            foreach(var id in topFileIds)
            {
                // Assuming FileEntry primary key allows lookup by ID - wait, FileEntry lacks explicit ID lookup in previous code?
                // FileEntry PK is FilePath? No, it has FileId (FRN).
                // But DatabaseService.GetFileEntryAsync uses path...
                // Let's verify FileEntry structure in next step or use Table<FileEntry>.Where(x => x.FileId == id)
                var entry = await _connection!.Table<FileEntry>().Where(x => x.FileId == long.Parse(id)).FirstOrDefaultAsync();
                if (entry != null) entries.Add(entry);
            }
            
            return entries;
        }

        private double CosineSimilarity(float[] v1, float[] v2)
        {
            if (v1.Length != v2.Length) return 0;

            double dot = 0.0;
            double mag1 = 0.0;
            double mag2 = 0.0;

            for (int i = 0; i < v1.Length; i++)
            {
                dot += v1[i] * v2[i];
                mag1 += v1[i] * v1[i];
                mag2 += v2[i] * v2[i];
            }

            return dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
        }

        public async Task<IEnumerable<FileEntry>> GetAllFilesAsync()
        {
            return await _connection!.Table<FileEntry>().ToListAsync();
        }

        public async Task AddIgnoreRuleAsync(IgnoreRule rule)
        {
            await _connection!.InsertOrReplaceAsync(rule);
        }

        public async Task RemoveIgnoreRuleAsync(string path)
        {
            await _connection!.DeleteAsync<IgnoreRule>(path);
        }

        public async Task<IEnumerable<IgnoreRule>> GetAllIgnoreRulesAsync()
        {
            return await _connection!.Table<IgnoreRule>().ToListAsync();
        }

        public async Task<List<string>> GetFileTagsAsync(string filePath)
        {
            var entry = await GetFileEntryAsync(filePath);
            if (entry == null) return new List<string>();

            var meta = await GetMetadataAsync(entry.FileId.ToString());
            if (meta == null || string.IsNullOrWhiteSpace(meta.Tags)) return new List<string>();

            return meta.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(t => t.Trim())
                            .ToList();
        }
        public async Task SaveMerkleTreeAsync(FileMerkleTree tree)
        {
            await _connection!.InsertOrReplaceAsync(tree);
        }

        public async Task<FileMerkleTree> GetMerkleTreeAsync(string filePath)
        {
            return await _connection!.Table<FileMerkleTree>().Where(mt => mt.FilePath == filePath).FirstOrDefaultAsync();
        }

        // Settings
        public async Task SaveSettingAsync(string key, string value)
        {
            await _connection!.InsertOrReplaceAsync(new AppSetting { Key = key, Value = value });
        }

        public async Task<string?> GetSettingAsync(string key)
        {
            var setting = await _connection!.Table<AppSetting>().Where(x => x.Key == key).FirstOrDefaultAsync();
            return setting?.Value;
        }

        // Scan State Persistence
        public async Task AddDirectoryToScanQueueAsync(string path, string driveLetter)
        {
            await _connection!.InsertAsync(new DirectoryScanState 
            { 
                Path = path, 
                DriveLetter = driveLetter, 
                IsProcessed = false, 
                QueuedAt = DateTime.Now 
            });
        }

        public async Task<DirectoryScanState?> GetNextDirectoryToScanAsync()
        {
            // FIDO-like queue: Get oldest unprocessed
            return await _connection!.Table<DirectoryScanState>()
                                     .Where(x => !x.IsProcessed)
                                     .OrderBy(x => x.QueuedAt)
                                     .FirstOrDefaultAsync();
        }

        public async Task MarkDirectoryAsProcessedAsync(int id)
        {
            var item = await _connection!.GetAsync<DirectoryScanState>(id);
            if (item != null)
            {
                // We delete processed items to keep table small, or we could mark them. 
                // For a pause/resume queue, deleting is usually cleaner for indefinite running.
                await _connection!.DeleteAsync(item);
            }
        }

        public async Task ClearScanQueueAsync()
        {
            await _connection!.DeleteAllAsync<DirectoryScanState>();
        }

        public async Task<int> GetPendingScanCountAsync()
        {
             return await _connection!.Table<DirectoryScanState>().CountAsync();
        }
    }
}
