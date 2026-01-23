using System;
using SQLite;

namespace SecureFileMonitor.Core.Models
{
    public class DirectoryScanState
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        [Indexed]
        public string Path { get; set; } = string.Empty;
        
        public bool IsProcessed { get; set; }
        
        public string DriveLetter { get; set; } = string.Empty;
        
        public DateTime QueuedAt { get; set; }
    }
}
