using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.Converters;

public class OperationCategoryToColorConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> CategoryColors = new()
    {
        ["Layer"] = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
        ["Xref"] = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
        ["Alignment"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
        ["Profile"] = new SolidColorBrush(Color.FromRgb(0x9C, 0x27, 0xB0)),
        ["ViewFrame"] = new SolidColorBrush(Color.FromRgb(0x00, 0xBC, 0xD4)),
        ["Viewport"] = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
        ["Block"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0x3B)),
        ["Detailing"] = new SolidColorBrush(Color.FromRgb(0x79, 0x55, 0x48)),
        ["DataShortcut"] = new SolidColorBrush(Color.FromRgb(0x60, 0x7D, 0x8B)),
        ["Style"] = new SolidColorBrush(Color.FromRgb(0xE9, 0x1E, 0x63)),
    };

    private static readonly SolidColorBrush DefaultColor = new(Color.FromRgb(0x4A, 0x55, 0x68));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string category && CategoryColors.TryGetValue(category, out var brush))
            return brush;
        return DefaultColor;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
