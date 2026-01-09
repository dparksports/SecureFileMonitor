using System;
using System.Threading;
using System.Threading.Tasks;

namespace SecureFileMonitor.Core.Services
{
    public interface IFileScannerService
    {
        Task ScanDriveAsync(string driveLetter, IProgress<string> progress, CancellationToken cancellationToken);
    }
}
