using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SecureFileMonitor.Core.Services
{
    public class VerifyingHasherService : IHasherService
    {
        private readonly IHasherService _primary;
        private readonly IHasherService _reference;
        private readonly ILogger _logger;

        public VerifyingHasherService(IHasherService primary, IHasherService reference, ILogger logger)
        {
            _primary = primary;
            _reference = reference;
            _logger = logger;
        }

        public async Task<string> ComputeHashAsync(string filePath)
        {
            var t1 = _primary.ComputeHashAsync(filePath);
            var t2 = _reference.ComputeHashAsync(filePath);
            
            await Task.WhenAll(t1, t2);

            if (t1.Result != t2.Result)
            {
                _logger.LogCritical($"[VERIFICATION FAILED] File: {filePath}");
                _logger.LogCritical($"Primary (GPU): {t1.Result}");
                _logger.LogCritical($"Reference (CPU): {t2.Result}");
                return t2.Result; // Return safe reference
            }

            return t1.Result;
        }

        public async Task<string> ComputeHashAsync(Stream stream)
        {
            // Stream cannot be read twice easily without seeking.
            // For verification, we likely need to copy it or standard CPU measures.
            // Since this is rare in our app (FileScanner uses paths), we'll skip complex stream verification or implement memory copy.
            // Fallback to primary for now to avoid stream position issues.
            return await _primary.ComputeHashAsync(stream);
        }

        public async Task<string[]> ComputeBlockHashesAsync(string filePath, int blockSize)
        {
            var t1 = _primary.ComputeBlockHashesAsync(filePath, blockSize);
            var t2 = _reference.ComputeBlockHashesAsync(filePath, blockSize);
            
            await Task.WhenAll(t1, t2);
            
            // Compare arrays
            bool match = true;
            if (t1.Result.Length != t2.Result.Length) match = false;
            else
            {
                for (int i=0; i<t1.Result.Length; i++)
                {
                    if (t1.Result[i] != t2.Result[i])
                    {
                        match = false; 
                        break;
                    }
                }
            }

            if (!match)
            {
                _logger.LogCritical($"BLOCK HASH MISMATCH detected for {filePath}");
                return t2.Result;
            }

            return t1.Result;
        }
    }
}
