using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FileBadger.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool))
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            return (bool)value
                ? Visibility.Visible
                : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility targetVisibility && targetVisibility == Visibility.Visible;
        }
    }
}
