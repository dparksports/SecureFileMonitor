using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SecureFileMonitor.Core.Services
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows6.2")]
    public class GpuHasherService : IHasherService
    {
        private readonly ILogger<GpuHasherService> _logger;

        public GpuHasherService(ILogger<GpuHasherService> logger)
        {
            _logger = logger;
        }

        // Static check for GPU availability using ComputeSharp
        public static bool IsGpuSupported
        {
            get
            {
                try
                {
                    return ComputeSharp.GraphicsDevice.GetDefault() != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task<string> ComputeHashAsync(string filePath)
        {
             // For now, we are just detecting the GPU but falling back to CPU logic 
             // to allow the code to compile and run even if the DLL is missing later (caught by try-catch in factory/main).
             throw new NotImplementedException();
        }

        public async Task<string> ComputeHashAsync(System.IO.Stream stream)
        {
             throw new NotImplementedException();
        }

        public async Task<string[]> ComputeBlockHashesAsync(string filePath, int blockSize)
        {
            // Placeholder logic to satisfy interface
            await Task.Delay(10);
            return Array.Empty<string>();
        }
    }
}
