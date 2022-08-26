using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DuplicateFileTool.Converters
{
    internal class IsNullOrWhitespaceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var isNullOrWhiteSpace = value is not string str || string.IsNullOrWhiteSpace(str);
            var isNegated = parameter is string parameterStr && parameterStr.Equals("Not", StringComparison.OrdinalIgnoreCase);
            return isNegated ? !isNullOrWhiteSpace : isNullOrWhiteSpace;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return "";
        }
    }
}
