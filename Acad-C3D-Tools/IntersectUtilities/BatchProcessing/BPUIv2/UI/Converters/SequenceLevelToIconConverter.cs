using System.Globalization;
using System.Windows.Data;
using IntersectUtilities.BatchProcessing.BPUIv2.Sequences;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.Converters;

public class SequenceLevelToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is SequenceStorageLevel level ? level switch
        {
            SequenceStorageLevel.Predefined => "\uE8A7",
            SequenceStorageLevel.User => "\uE77B",
            SequenceStorageLevel.Shared => "\uE902",
            _ => "\uE7C3"
        } : "\uE7C3";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
