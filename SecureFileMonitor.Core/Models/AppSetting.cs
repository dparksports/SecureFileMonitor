using SQLite;

namespace SecureFileMonitor.Core.Models
{
    public class AppSetting
    {
        [PrimaryKey]
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
