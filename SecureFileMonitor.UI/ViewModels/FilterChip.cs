using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SecureFileMonitor.UI.ViewModels
{
    public partial class FilterChip : ObservableObject
    {
        public string Extension { get; set; } = string.Empty;
        public int Count { get; set; }

        [ObservableProperty]
        private bool _isChecked = true;

        public string Description => $"{Extension} ({Count})";

        // Optional: Reference to parent VM to trigger filter update? 
        // Or ViewModel subscribes to PropertyChanged of items in collection.
    }
}
