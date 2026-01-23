using System;
using System.Threading;
using System.Threading.Tasks;

namespace SecureFileMonitor.Core.Services
{
    public interface IFileScannerService
    {
        Task ScanDriveAsync(string driveLetter, bool scanReparseFolders, bool forceFullScan, IProgress<(string Status, double? Percent, TimeSpan? ETA)> progress, CancellationToken cancellationToken);
        
        // Background Hashing for Large Files
        event EventHandler<(int Remaining, int Total, TimeSpan? ETA)>? OnHashProgressChanged;
        Task StartBackgroundHashingAsync(IProgress<(string Status, int Remaining, int Total, TimeSpan? ETA)> progress, CancellationToken cancellationToken);
        int GetPendingHashCount();
        void SetPaused(bool isPaused);
    }
}
