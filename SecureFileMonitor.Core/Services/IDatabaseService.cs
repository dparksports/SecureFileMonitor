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
        
        Task SaveMetadataAsync(FileMetadata metadata);
        Task<FileMetadata> GetMetadataAsync(string fileId);
        Task<IEnumerable<FileEntry>> SearchFilesAsync(float[] queryVector, int limit = 10);
        Task<IEnumerable<FileEntry>> GetAllFilesAsync();

        // Ignore Rules
        Task AddIgnoreRuleAsync(IgnoreRule rule);
        Task RemoveIgnoreRuleAsync(string path);
        Task<IEnumerable<IgnoreRule>> GetAllIgnoreRulesAsync();
        
        Task<List<string>> GetFileTagsAsync(string filePath);

        // Transcription History
        Task SaveTranscriptionTaskAsync(TranscriptionTask task);
        Task<IEnumerable<TranscriptionTask>> GetAllTranscriptionTasksAsync();
        
        // Ignored Processes
        Task AddIgnoredProcessAsync(string processName);
        Task RemoveIgnoredProcessAsync(string processName);
        Task<IEnumerable<string>> GetIgnoredProcessesAsync();
    }
}
