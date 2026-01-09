using System.Collections.Generic;
using System.Threading.Tasks;
using SecureFileMonitor.Core.Models;

namespace SecureFileMonitor.Core.Services
{
    public interface IDatabaseService
    {
        Task InitializeAsync(string password);
        Task SaveFileEntryAsync(FileEntry entry);
        Task<FileEntry> GetFileEntryAsync(string filePath);
        Task<IEnumerable<FileEntry>> GetAllEntriesAsync();
        Task<IEnumerable<FileEntry>> GetDuplicateFilesAsync();
        Task SaveAuditLogAsync(FileActivityEvent activity);
    }
}
