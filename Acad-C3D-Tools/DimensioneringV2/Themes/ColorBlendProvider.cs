using Mapsui.Styles;
using Mapsui.Styles.Thematics;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Themes
{
    internal static class ColorBlendProvider
    {
        internal static ColorBlend Standard => BlueYellowRed;
        internal static ColorBlend BlueYellowRed => GetColorBlend([Color.Blue, Color.Yellow, Color.Red]);
        internal static ColorBlend GetColorBlend(Color[] colors)
        {
            var positions = Enumerable.Range(0, colors.Length).Select(i => (double)i / (colors.Length - 1)).ToArray();
            return new ColorBlend(colors, positions);
        }
    }
}
