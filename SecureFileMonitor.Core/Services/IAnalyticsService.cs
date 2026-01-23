using System.Collections.Generic;
using System.Threading.Tasks;

namespace SecureFileMonitor.Core.Services
{
    public interface IAnalyticsService
    {
        Task LogEventAsync(string eventName, Dictionary<string, object>? parameters = null);
        bool IsEnabled { get; set; }
    }
}
