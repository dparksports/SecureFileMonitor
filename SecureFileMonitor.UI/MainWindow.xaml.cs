using System.Windows;
using SecureFileMonitor.UI.ViewModels;


namespace SecureFileMonitor.UI
{
    public partial class MainWindow : Window // Should be FluentWindow if using Wpf.Ui fully, but Window is safer for standard template
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}