using Mapsui;
using Mapsui.Rendering.Skia.SkiaWidgets;
using Mapsui.Widgets;

using SkiaSharp;

namespace DimensioneringV2.Legend
{
    /// <summary>
    /// SkiaSharp renderer for <see cref="StatusWidget"/>.
    /// Positions the content at the bottom-left of the viewport,
    /// offset by the widget's margin values.
    /// </summary>
    internal class StatusWidgetSkiaRenderer : ISkiaWidgetRenderer
    {
        public void Draw(SKCanvas canvas, Viewport viewport, IWidget widget, float layerOpacity)
        {
            if (widget is not StatusWidget sw) return;
            if (!sw.Enabled) return;
            if (sw.Content == null) return;

            using var ctx = new LegendRenderContext();

            var size = sw.Content.Measure(ctx);

            // Bottom-left positioning: X from left edge, Y from bottom edge
            float x = sw.MarginX;
            float y = (float)viewport.Height - sw.MarginY - size.Height;

            var bounds = new SKRect(x, y, x + size.Width, y + size.Height);
            sw.Content.Draw(canvas, bounds, ctx);
        }
    }
}
