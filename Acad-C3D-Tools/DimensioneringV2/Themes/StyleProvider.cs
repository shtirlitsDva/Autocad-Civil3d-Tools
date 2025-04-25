using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Themes
{
    internal static class StyleProvider
    {
        public static IStyle BasicStyle { get; } = new VectorStyle
        {
            Line = new Pen(Color.Black) { Width = 1.5 }
        };
    }    
}
