using Mapsui;
using Mapsui.Widgets;

namespace DimensioneringV2.Legend
{
    internal class LegendWidget : IWidget
    {
        public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
        public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;
        public float MarginX { get; set; } = 10;
        public float MarginY { get; set; } = 10;
        public MRect? Envelope { get; set; }
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// The root element of the legend's visual tree.
        /// Typically a <see cref="StackPanel"/> containing text blocks, item lists, etc.
        /// </summary>
        public LegendElement? Content { get; set; }

        public bool HandleWidgetTouched(Navigator navigator, MPoint position)
        {
            navigator.CenterOn(0, 0);
            // We don't need touch handling for now
            return false;
        }
    }
}
