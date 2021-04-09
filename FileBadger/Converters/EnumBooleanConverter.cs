using System;
using System.Globalization;
using System.Windows.Data;

namespace FileBadger.Converters
{
    public class EnumBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            var enumConvertedToString = value.ToString();
            var enumAsString = parameter.ToString();
            return enumConvertedToString.Equals(enumAsString, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return null;

            var isSettingValue = (bool)value;
            var enumAsString = parameter.ToString();
            return isSettingValue
                ? Enum.Parse(targetType, enumAsString)
                : null;
        }
    }
}
