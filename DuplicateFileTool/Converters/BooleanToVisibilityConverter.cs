using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DuplicateFileTool.Converters;

public sealed class BooleanToVisibilityConverter : IValueConverter
{
    private readonly struct Parameters
    {
        public bool IsInverted { get; init; }
        public bool IsCollapse { get; init; }
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool sourceBool)
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        var parameters = GetParameters(parameter);
        if (parameters.IsInverted)
            sourceBool = !sourceBool;

        if (sourceBool)
            return Visibility.Visible;
        return parameters.IsCollapse
            ? Visibility.Collapsed
            : Visibility.Hidden;
    }

    private static Parameters GetParameters(object? parametersObject) => parametersObject is string parameters
        ? new Parameters { IsInverted = parameters.Contains("Invert", StringComparison.OrdinalIgnoreCase), IsCollapse = parameters.Contains("Collapse", StringComparison.OrdinalIgnoreCase) }
        : new Parameters();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => 
        value is Visibility.Visible;
}