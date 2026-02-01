using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;

namespace SecureFileMonitor.UI.ViewModels
{
    public partial class FolderNode : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public ObservableCollection<FolderNode> Children { get; set; } = new();

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isExpanded;

        private int _fileCount;
        public int FileCount
        {
            get => _fileCount;
            set
            {
                if (SetProperty(ref _fileCount, value))
                {
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public string DisplayName => $"{Name} ({FileCount})";
        
        // Helper to find or create child
        public FolderNode GetOrCreateChild(string name, string fullPath)
        {
            var child = Children.FirstOrDefault(c => c.Name == name);
            if (child == null)
            {
                child = new FolderNode { Name = name, FullPath = fullPath };
                // Insert alphabetically
                int index = 0;
                while (index < Children.Count && string.Compare(Children[index].Name, name, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    index++;
                }
                Children.Insert(index, child);
            }
            return child;
        }
    }
}
