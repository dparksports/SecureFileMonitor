using System;
using SQLite;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SecureFileMonitor.Core.Models
{
    public partial class FileActivityEvent : ObservableObject
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [ObservableProperty]
        private DateTime _timestamp;

        public int ProcessId { get; set; }
        
        [ObservableProperty]
        private string _processName = string.Empty;

        [Indexed]
        public string FilePath { get; set; } = string.Empty;

        [ObservableProperty]
        private FileOperation _operation;

        [ObservableProperty]
        private string _userName = string.Empty;
    }

    public enum FileOperation
    {
        Unknown,
        Create,
        Read,
        Write,
        Delete,
        Rename
    }
}
