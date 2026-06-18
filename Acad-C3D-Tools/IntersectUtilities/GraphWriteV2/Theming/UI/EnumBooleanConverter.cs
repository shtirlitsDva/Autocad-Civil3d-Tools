using System;
using System.Globalization;
using System.Windows.Data;

namespace IntersectUtilities.GraphWriteV2.Theming.UI
{
    /// <summary>
    /// Binds a RadioButton's IsChecked to one value of an enum property: checked when the property
    /// equals the ConverterParameter (the enum member name). The standard WPF idiom for rendering a
    /// single-select list / segmented control from an enum-valued view-model property.
    /// </summary>
    internal sealed class EnumBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null || parameter is not string name) return false;
            return string.Equals(value.ToString(), name, StringComparison.Ordinal);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is true && parameter is string name)
            {
                var t = Nullable.GetUnderlyingType(targetType) ?? targetType;
                if (t == typeof(bool) && bool.TryParse(name, out var bv)) return bv;
                try { return Enum.Parse(t, name); } catch { return Binding.DoNothing; }
            }
            return Binding.DoNothing;
        }
    }
}
