using SQLite;

namespace SecureFileMonitor.Core.Models
{
    public class FileMetadata
    {
        [PrimaryKey]
        public string FileId { get; set; } = string.Empty; // Foreign Key to FileEntry (FRN)

        public string Transcription { get; set; } = string.Empty;
        public string AiDescription { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty; // User defined tags
        
        // Storing embeddings as a serialized blob or JSON string for now
        // A 384-dim float array takes ~1.5KB
        public string VectorEmbedding { get; set; } = string.Empty; 

        public DateTime LastAnalyzed { get; set; }
    }
}
