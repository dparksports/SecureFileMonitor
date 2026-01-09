using System.IO;
using System.Threading.Tasks;

namespace SecureFileMonitor.Core.Services
{
    public interface IHasherService
    {
        Task<string> ComputeHashAsync(string filePath);
        Task<string> ComputeHashAsync(Stream stream);
        Task<string[]> ComputeBlockHashesAsync(string filePath, int blockSize);
    }
}
