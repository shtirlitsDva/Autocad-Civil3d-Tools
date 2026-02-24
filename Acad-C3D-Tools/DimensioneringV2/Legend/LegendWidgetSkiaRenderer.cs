using Mapsui;
using Mapsui.Rendering.Skia.SkiaWidgets;
using Mapsui.Widgets;

using SkiaSharp;

namespace DimensioneringV2.Legend
{
    /// <summary>
    /// SkiaSharp renderer for <see cref="LegendWidget"/>.
    /// Delegates all layout and drawing to the element tree via Measure/Draw.
    /// </summary>
    internal class LegendWidgetSkiaRenderer : ISkiaWidgetRenderer
    {
        public void Draw(SKCanvas canvas, Viewport viewport, IWidget widget, float layerOpacity)
        {
            if (widget is not LegendWidget lw) return;
            if (!lw.Enabled) return;
            if (lw.Content == null) return;

            using var ctx = new LegendRenderContext();

            var size = lw.Content.Measure(ctx);
            var bounds = new SKRect(
                lw.MarginX,
                lw.MarginY,
                lw.MarginX + size.Width,
                lw.MarginY + size.Height);

            lw.Content.Draw(canvas, bounds, ctx);
        }
    }
}
