using System;

namespace SecureFileMonitor.UI.ViewModels
{
    public class ActivitySession
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int FileCount { get; set; }
        public double Intensity { get; set; } // 0.0 to 1.0 for heatmap color
        
        public string DisplayTime => StartTime.ToString("MMM dd HH:mm");
        public string Tooltip => $"{FileCount} changes around {DisplayTime}";
    }
}
