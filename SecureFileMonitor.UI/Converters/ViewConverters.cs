using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SecureFileMonitor.UI.Converters
{
    public class ViewToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
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
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return System.Windows.Media.Brushes.Transparent;
            // Highlight color if active: #1E88E5 blue
            return value.ToString() == parameter.ToString() 
                ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#1E88E5") 
                : System.Windows.Media.Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
