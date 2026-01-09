using System.Collections.Generic;
using SecureFileMonitor.Core.Models;

namespace SecureFileMonitor.Core.Services
{
    public interface IUsnJournalService
    {
        void Initialize(string driverLetter);
        IEnumerable<FileEntry> ReadChanges();
        long GetCurrentUsn();
    }
}
