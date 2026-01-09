using System;
using SecureFileMonitor.Core.Models;

namespace SecureFileMonitor.Core.Services
{
    public interface IEtwMonitorService
    {
        void Start();
        void Stop();
        event EventHandler<FileActivityEvent> OnFileActivity;
    }
}
