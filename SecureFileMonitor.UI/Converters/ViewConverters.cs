using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SecureFileMonitor.UI.Converters
{
    public class ViewToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return Visibility.Collapsed;
            return value.ToString() == parameter.ToString() ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ViewToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return System.Windows.Media.Brushes.Transparent;
            // Highlight color if active: #1E88E5 blue
            if (value.ToString() == parameter.ToString())
            {
                var brush = new System.Windows.Media.BrushConverter().ConvertFrom("#1E88E5") as System.Windows.Media.Brush;
                return brush ?? System.Windows.Media.Brushes.Transparent;
            }
            return System.Windows.Media.Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FileSizeConverter : IValueConverter
    {
        private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is long size)
            {
                if (size == 0) return "0 B";
                int unitIndex = 0;
                double doubleSize = size;
                while (doubleSize >= 1024 && unitIndex < Units.Length - 1)
                {
                    doubleSize /= 1024;
                    unitIndex++;
                }
                return $"{doubleSize:F2} {Units[unitIndex]}";
            }
            return "0 B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
