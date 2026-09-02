using System.Globalization;

namespace AcadOverrules.VertexCircles.UI
{
    /// <summary>
    /// Lenient number parsing for the text boxes in the settings window. The users type on a
    /// Danish keyboard, so "0,05" and "0.05" must both work regardless of what WPF's binding
    /// culture happens to be.
    /// </summary>
    internal static class NumberText
    {
        public static bool TryParse(string? text, out double value)
        {
            value = 0.0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return true;

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;

            //Neither culture took it. The usual cause is a decimal separator that matches
            //neither - a machine can run a Danish locale that is configured with a decimal
            //point, and then a typed comma is invalid everywhere. Swapping it makes the field
            //accept whichever separator the user reaches for. Anything with more than one
            //separator still fails, as it should.
            return double.TryParse(
                text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static string Format(double value) =>
            value.ToString("0.####", CultureInfo.CurrentCulture);
    }
}
