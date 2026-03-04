using Mapsui;
using Mapsui.Widgets;

namespace DimensioneringV2.Legend
{
    /// <summary>
    /// A lightweight widget for displaying modal status text at the bottom-left
    /// of the map viewport. Reuses the <see cref="LegendElement"/> tree for content.
    /// Typically enabled only while the user is in a special interactive mode
    /// (e.g. AngivDim pipe-selection).
    /// </summary>
    internal class StatusWidget : IWidget
    {
        public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
        public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Bottom;
        public float MarginX { get; set; } = 10;
        public float MarginY { get; set; } = 10;
        public MRect? Envelope { get; set; }
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// The root element of the status visual tree.
        /// Typically a <see cref="StackPanel"/> with a title and instruction text.
        /// </summary>
        public LegendElement? Content { get; set; }

        public bool HandleWidgetTouched(Navigator navigator, MPoint position) => false;
    }
}
