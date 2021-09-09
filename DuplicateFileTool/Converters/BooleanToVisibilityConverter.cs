using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DuplicateFileTool.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        private struct Parameters
        {
            public bool IsInverted { get; set; }
            public bool IsCollapse { get; set; }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool sourceBool)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            var parameters = GetParameters(parameter);
            if (parameters.IsInverted)
                sourceBool = !sourceBool;

            return sourceBool
                ? Visibility.Visible
                : parameters.IsCollapse
                    ? Visibility.Collapsed
                    : Visibility.Hidden;
        }

        private static Parameters GetParameters(object parametersObject)
        {
            return parametersObject is string parameters
                ? new Parameters { IsInverted = parameters.IndexOf("Invert", StringComparison.OrdinalIgnoreCase) != -1, IsCollapse = parameters.IndexOf("Collapse", StringComparison.OrdinalIgnoreCase) != -1 }
                : new Parameters();
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility.Visible;
        }
    }
}