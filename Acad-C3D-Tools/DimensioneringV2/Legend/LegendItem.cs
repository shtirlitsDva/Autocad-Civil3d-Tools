using SkiaSharp;
using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Legend
{
    internal class LegendItem
    {
        public string Label { get; set; } = string.Empty;
        public SKBitmap? SymbolBitmap { get; set; }
        public Color? SymbolColor { get; set; }
        public double SymbolWidth { get; set; }
    }
}