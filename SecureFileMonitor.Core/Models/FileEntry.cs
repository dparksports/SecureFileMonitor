using System;
using SQLite;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SecureFileMonitor.Core.Models
{
    public partial class FileEntry : ObservableObject
    {
        [PrimaryKey]
        public string FilePath { get; set; } = string.Empty;

        [Ignore]
        public string DirectoryName => System.IO.Path.GetDirectoryName(FilePath) ?? "";
        
        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isIgnored;

        public long FileId { get; set; } // FRN
        public long ParentId { get; set; } // Parent FRN
        public long Usn { get; set; }
        public string FileName { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long FileSize { get; set; }
        public string FileExtension { get; set; } = string.Empty;
        
        public DateTime CreationTime { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime LastAccessTime { get; set; }
        
        // Integrity
        public string? CurrentHash { get; set; }
        public string? LastVerifiedHash { get; set; }
        
        public uint FileAttributes { get; set; }

        [Ignore]
        public string FileType => FileExtension.TrimStart('.').ToUpper() switch 
        {
            "" => "File",
            "EXE" or "MSI" or "BAT" or "CMD" or "PS1" => "Application",
            "TXT" or "MD" or "LOG" or "JSON" or "XML" or "CSV" or "CONFIG" => "Text Document",
            "DLL" or "SYS" or "BIN" or "DAT" => "System File",
            "MP4" or "MKV" or "AVI" or "MOV" or "WMV" => "Video",
            "MP3" or "WAV" or "FLAC" or "M4A" => "Audio",
            "JPG" or "JPEG" or "PNG" or "GIF" or "BMP" or "ICO" => "Image",
            "PDF" => "PDF Document",
            "DOC" or "DOCX" or "XLS" or "XLSX" or "PPT" or "PPTX" => "Office Document",
            "ZIP" or "RAR" or "7Z" or "TAR" or "GZ" => "Archive",
            _ => FileExtension.TrimStart('.').ToUpper() + " File"
        };
    }
}
