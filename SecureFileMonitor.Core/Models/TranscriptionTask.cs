using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SecureFileMonitor.Core.Models
{
    public enum TranscriptionStatus
    {
        Queued,
        Processing,
        Completed,
        Error
    }

    public partial class TranscriptionTask : ObservableObject
    {
        [ObservableProperty]
        private string _id = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private TranscriptionStatus _status = TranscriptionStatus.Queued;

        [ObservableProperty]
        private double _progress;

        [ObservableProperty]
        private string _transcript = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private DateTime _queuedAt = DateTime.Now;

        [ObservableProperty]
        private DateTime? _completedAt;
    }
}
