using System.Globalization;
using System.Windows.Data;
using NorsynHydraulicTester.Services;

namespace NorsynHydraulicTester.Converters;

public class LaTeXSanitizeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string latex && !string.IsNullOrEmpty(latex))
            return LaTeXFormatter.Sanitize(latex);
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
