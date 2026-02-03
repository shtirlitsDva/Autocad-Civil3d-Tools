using System.Globalization;
using System.Windows.Data;

namespace NorsynHydraulicTester.Converters;

public class DanishNumberConverter : IValueConverter
{
    private static readonly CultureInfo DanishCulture = new("da-DK");

    public string Format { get; set; } = "N2";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string format = parameter as string ?? Format;

        return value switch
        {
            double d => d.ToString(format, DanishCulture),
            float f => f.ToString(format, DanishCulture),
            decimal dec => dec.ToString(format, DanishCulture),
            int i => i.ToString(format, DanishCulture),
            _ => value?.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
            return 0.0;

        if (double.TryParse(s, NumberStyles.Any, DanishCulture, out double result))
            return result;

        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            return result;

        return 0.0;
    }
}

public class UnitDisplayConverter : IMultiValueConverter
{
    private static readonly CultureInfo DanishCulture = new("da-DK");

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return string.Empty;

        var value = values[0];
        var unit = values[1] as string ?? "";
        var format = parameter as string ?? "N2";

        string formattedValue = value switch
        {
            double d => d.ToString(format, DanishCulture),
            float f => f.ToString(format, DanishCulture),
            int i => i.ToString(format, DanishCulture),
            _ => value?.ToString() ?? "0"
        };

        return string.IsNullOrEmpty(unit) ? formattedValue : $"{formattedValue} {unit}";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
