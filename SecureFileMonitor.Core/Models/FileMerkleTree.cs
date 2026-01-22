using SQLite;

namespace SecureFileMonitor.Core.Models
{
    public class FileMerkleTree
    {
        [PrimaryKey]
        public string FilePath { get; set; } = string.Empty;
        
        public string SerializedTree { get; set; } = string.Empty; // JSON
        public DateTime LastUpdated { get; set; }
    }
}
