using System;
using System.Globalization;
using System.Windows.Data;

namespace DuplicateFileTool.Converters
{
    internal class EnumObjectToEnumerationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Array.Empty<object>();

            var enumObjType = value.GetType();
            return enumObjType.IsEnum
                ? Enum.GetValues(enumObjType)
                : Array.Empty<object>();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
