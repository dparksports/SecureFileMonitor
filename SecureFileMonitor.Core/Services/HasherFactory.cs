using System;
using Microsoft.Extensions.Logging;

namespace SecureFileMonitor.Core.Services
{
    public class HasherFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<GpuHasherService> _gpuLogger;

        public HasherFactory(IServiceProvider serviceProvider, ILogger<GpuHasherService> gpuLogger)
        {
            _serviceProvider = serviceProvider;
            _gpuLogger = gpuLogger;
        }

        public IHasherService Create(bool useGpu, bool useThreads, bool verify = false)
        {
            IHasherService primary;

            if (useGpu)
            {
                if (OperatingSystem.IsWindowsVersionAtLeast(6, 2))
                {
                    try 
                    {
                        primary = new GpuHasherService(_gpuLogger); 
                    }
                    catch 
                    { 
                        primary = new CpuHasherService(); 
                    }
                }
                else
                {
                    primary = new CpuHasherService(); // Fallback on old OS
                }
            }
            else if (useThreads)
            {
                primary = new ThreadedHasherService();
            }
            else
            {
                primary = new CpuHasherService();
            }

            if (verify)
            {
                // Reference is always CPU
                var reference = new CpuHasherService();
                return new VerifyingHasherService(primary, reference, _gpuLogger); // Reusing logger or creating new? Using generic logger 
            }

            return primary;
        }
    }
}
