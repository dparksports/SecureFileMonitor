using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;

namespace SecureFileMonitor.UI.ViewModels
{
    public partial class DriveViewModel : ObservableObject
    {
        public string Name { get; }
        public string Label { get; }
        public string TotalSize { get; }
        public string FreeSpace { get; }

        [ObservableProperty]
        private bool _isSelected = true;

        public DriveViewModel(DriveInfo drive)
        {
            Name = drive.Name;
            try 
            {
                Label = drive.VolumeLabel;
                TotalSize = FormatSize(drive.TotalSize);
                FreeSpace = FormatSize(drive.TotalFreeSpace);
                
                if (drive.TotalSize > 0)
                {
                    UsagePercentage = (1.0 - ((double)drive.TotalFreeSpace / drive.TotalSize)) * 100;
                }
            }
            catch 
            {
                Label = "Unknown";
                TotalSize = "?";
                FreeSpace = "?";
                UsagePercentage = 0;
            }
        }

        public double UsagePercentage { get; private set; }

        private string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.0} {sizes[order]}";
        }
    }
}
