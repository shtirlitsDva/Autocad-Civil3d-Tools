using Mapsui;
using Mapsui.Widgets;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Legend
{
    internal class LegendWidget : IWidget
    {
        public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
        public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;
        public float MarginX { get; set; } = 20;
        public float MarginY { get; set; } = 20;
        public MRect? Envelope { get; set; }
        public bool Enabled { get; set; } = true;

        public ILegendData? LegendData { get; set; }

        public bool HandleWidgetTouched(Navigator navigator, MPoint position)
        {
            navigator.CenterOn(0, 0);
            // We don't need touch handling for now
            return false;
        }
    }

    internal enum LegendType
    {
        None,
        Categorical,
        Gradient
    }
}