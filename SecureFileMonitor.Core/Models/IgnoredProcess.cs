using SQLite;

namespace SecureFileMonitor.Core.Models
{
    public class IgnoredProcess
    {
        [PrimaryKey]
        public string ProcessName { get; set; } = string.Empty;
    }
}
