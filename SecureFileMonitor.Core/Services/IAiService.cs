using System.Threading.Tasks;
using SecureFileMonitor.Core.Models;

namespace SecureFileMonitor.Core.Services
{
    public interface IAiService
    {
        Task<bool> IsModelAvailableAsync();
        Task DownloadModelsAsync(System.IProgress<string> progress); // Downloads Whisper/VLM models if missing
        
        Task<string> TranscribeAudioAsync(string filePath, string modelType = "base", bool isEnglishOnly = true, bool useGpu = true);
        Task<string> GenerateVideoDescriptionAsync(string filePath);
        Task<string> GenerateEmbeddingAsync(string text);
        bool IsCudaAvailable { get; }
    }
}
