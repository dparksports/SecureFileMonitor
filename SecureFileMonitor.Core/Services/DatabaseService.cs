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
        private SQLiteAsyncConnection _connection;
        private readonly string _dbPath;

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

            await _connection.CreateTableAsync<FileEntry>();
            await _connection.CreateTableAsync<FileActivityEvent>(); // Need to ensure Models are SQLite generic compatible
        }

        public async Task SaveFileEntryAsync(FileEntry entry)
        {
            // Upsert
            var existing = await _connection.Table<FileEntry>().Where(x => x.FilePath == entry.FilePath).FirstOrDefaultAsync();
            if (existing != null)
            {
                entry.FileId = existing.FileId; // Keep ID? Wait, FileEntry has FRN as ID.
                // Actually, FileEntry key should be FilePath for lookup, but FRN for filesystem identity.
                // SQLite needs a Primary Key. Let's assume FileEntry defines one or we add [PrimaryKey] to Model.
                await _connection.UpdateAsync(entry);
            }
            else
            {
                await _connection.InsertAsync(entry);
            }
        }

        public async Task<FileEntry> GetFileEntryAsync(string filePath)
        {
            return await _connection.Table<FileEntry>().Where(x => x.FilePath == filePath).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<FileEntry>> GetAllEntriesAsync()
        {
            return await _connection.Table<FileEntry>().ToListAsync();
        }

        public async Task<IEnumerable<FileEntry>> GetDuplicateFilesAsync()
        {
            // Naive approach since sqlite-net doesn't support easy GROUP BY HAVING via LINQ
            var all = await _connection.Table<FileEntry>().ToListAsync();
            var duplicates = all.GroupBy(x => x.CurrentHash)
                                .Where(g => g.Count() > 1 && !string.IsNullOrEmpty(g.Key))
                                .SelectMany(g => g)
                                .ToList();
            return duplicates;
        }

        public async Task SaveAuditLogAsync(FileActivityEvent activity)
        {
            await _connection.InsertAsync(activity);
        }
    }
}
