using System.Collections.Generic;
using SecureFileMonitor.Core.Models;

namespace SecureFileMonitor.Core.Services
{
    public interface IUsnJournalService
    {
        void Initialize(string driveLetter);
        Task InitializeAllDrivesAsync();
        IEnumerable<FileEntry> ReadChanges(string driveLetter);
        long GetCurrentUsn(string driveLetter);
    }
}
