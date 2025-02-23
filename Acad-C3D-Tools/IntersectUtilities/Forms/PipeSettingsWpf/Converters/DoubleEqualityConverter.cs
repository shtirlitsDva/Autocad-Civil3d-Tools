using System;
using System.Globalization;
using System.Windows.Data;

namespace IntersectUtilities.Forms.PipeSettingsWpf.Converters
{
    public class DoubleEqualityConverter : IValueConverter
    {
        // Convert from double (SelectedOption) to bool (IsChecked)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double selected && parameter is double candidate)
            {
                // "equal enough" comparison
                return Math.Abs(selected - candidate) < 0.000001;
            }
            return false;
        }

        // ConvertBack from bool (RadioButton IsChecked) to double (the chosen option)
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter is double candidate)
            {
                return candidate;
            }
            return Binding.DoNothing;
        }
    }
}
