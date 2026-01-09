using System;
using SecureFileMonitor.Core.Models;

namespace SecureFileMonitor.Core.Services
{
    public interface IEtwMonitorService
    {
        void Start();
        void Stop();
        bool IgnoreInternalDatabase { get; set; }
        event EventHandler<FileActivityEvent> OnFileActivity;
    }
}
