using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DimensioneringV2.UI.PipeSettings
{
    /// <summary>
    /// Converts a PipeType to an array of available DN values.
    /// Uses PipeTypes from the window's DataContext.
    /// </summary>
    public class PipeTypeToDnValuesConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && 
                values[0] is PipeType pipeType && 
                values[1] is PipeTypes pipeTypes)
            {
                return pipeTypes.GetAvailableDnValues(pipeType);
            }
            return Array.Empty<int>();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
