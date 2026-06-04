using System.Globalization;
using System.Windows.Data;

namespace DuplicateFileTool.Converters;

internal sealed class LongConverter : IValueConverter
{
    //Source to target
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => 
        value is long longValue ? longValue.ToString() : "";

    //Target to source
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => 
        value is string stringValue && long.TryParse(new string(stringValue.Where(ch => ch >= 0x30 && ch <= 0x39).ToArray()), out var parsedValue) ? parsedValue : 0L;
}