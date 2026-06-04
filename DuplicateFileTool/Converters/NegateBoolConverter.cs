using System.Globalization;
using System.Windows.Data;

namespace DuplicateFileTool.Converters;

internal sealed class NegateBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => 
        value is bool boolValue ? !boolValue : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => 
        value is bool boolValue ? !boolValue : null;
}