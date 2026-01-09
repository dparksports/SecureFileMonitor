using System;
using SQLite;

namespace SecureFileMonitor.Core.Models
{
    public class FileActivityEvent
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public FileOperation Operation { get; set; }
        public string UserName { get; set; } = string.Empty;
    }

    public enum FileOperation
    {
        Unknown,
        Create,
        Read,
        Write,
        Delete,
        Rename
    }
}
