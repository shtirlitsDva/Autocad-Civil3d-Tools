using System.Drawing;

namespace AcadOverrules.VertexCircles
{
    /// <summary>
    /// The fixed marker colour is persisted as an HTML colour string ("#FF00FF") so the
    /// settings file stays readable and hand editable. Conversion is left to
    /// <see cref="ColorTranslator"/> rather than a hand rolled parser.
    /// </summary>
    internal static class HtmlColor
    {
        public static Color Parse(string? html)
        {
            if (string.IsNullOrWhiteSpace(html)) return Fallback;

            try
            {
                return ColorTranslator.FromHtml(html.Trim());
            }
            catch (System.Exception)
            {
                return Fallback;
            }
        }

        public static string Format(Color color) => ColorTranslator.ToHtml(color);

        /// <summary>Vivid magenta - visible on both a dark and a light background.</summary>
        public static Color Fallback => Color.FromArgb(255, 0, 255);
    }
}
