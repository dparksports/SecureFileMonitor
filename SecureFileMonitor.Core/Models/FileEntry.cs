using System;
using SQLite;

namespace SecureFileMonitor.Core.Models
{
    public class FileEntry
    {
        [PrimaryKey]
        public string FilePath { get; set; } = string.Empty;
        public long FileId { get; set; } // FRN
        public long ParentId { get; set; } // Parent FRN
        public long Usn { get; set; }
        public string FileName { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long FileSize { get; set; }
        public string FileExtension { get; set; } = string.Empty;
        
        public DateTime CreationTime { get; set; }
        public DateTime LastModified { get; set; }
        
        // Integrity
        public string? CurrentHash { get; set; }
        public string? LastVerifiedHash { get; set; }
        
        public uint FileAttributes { get; set; }
    }
}
