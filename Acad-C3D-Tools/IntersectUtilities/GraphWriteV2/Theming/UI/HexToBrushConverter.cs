using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace IntersectUtilities.GraphWriteV2.Theming.UI
{
    /// <summary>Converts a "#rrggbb" hex string to a frozen SolidColorBrush for the color swatches.</summary>
    internal sealed class HexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    return brush;
                }
                catch { }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
