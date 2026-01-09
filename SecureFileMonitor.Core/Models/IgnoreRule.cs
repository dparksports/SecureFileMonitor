using SQLite;

namespace SecureFileMonitor.Core.Models
{
    public class IgnoreRule
    {
        [PrimaryKey]
        public string Path { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
    }
}
