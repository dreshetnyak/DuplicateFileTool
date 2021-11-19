using System;
using System.Globalization;
using System.Windows.Data;

namespace DuplicateFileTool.Converters
{
    internal class NegateBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool boolValue ? !boolValue : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool boolValue ? !boolValue : null;
        }
    }
}
