using IntersectUtilities.UtilsCommon;

using Mapsui.Styles;

using SkiaSharp;

using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Legend
{
    /// <summary>
    /// A list of categorical legend items — each row is a color swatch (or bitmap) plus a label.
    /// Compound element that encapsulates the repeating item layout.
    /// </summary>
    internal class ItemList : LegendElement
    {
        public IList<LegendItem> Items { get; init; } = [];
        public float ItemHeight { get; init; } = 18f;
        public float LineLength { get; init; } = 30f;
        public float LabelGap { get; init; } = 10f;

        private List<LegendItem> GetVisibleItems()
            => Items.Where(x => x.Label.IsNotNoE()).ToList();

        protected override SKSize MeasureCore(LegendRenderContext ctx)
        {
            var visible = GetVisibleItems();
            if (visible.Count == 0) return SKSize.Empty;

            var textPaint = ctx.GetTextPaint(14f, true, LegendTextAlign.Left, new Color(0, 0, 0));
            float maxTextWidth = visible.Select(x => textPaint.MeasureText(x.Label)).Max();
            float width = LineLength + LabelGap + maxTextWidth;
            float height = visible.Count * ItemHeight;

            return new SKSize(width, height);
        }

        protected override void DrawCore(SKCanvas canvas, SKRect bounds, LegendRenderContext ctx)
        {
            var visible = GetVisibleItems();
            if (visible.Count == 0) return;

            var textPaint = ctx.GetTextPaint(14f, true, LegendTextAlign.Left, new Color(0, 0, 0));
            var metrics = ctx.GetMetrics(14f, true);

            float textHeight = metrics.Descent - metrics.Ascent;
            float baselineY = (ItemHeight - textHeight) / 2 - metrics.Ascent;

            float x = bounds.Left;
            float y = bounds.Top;

            foreach (var item in visible)
            {
                float centerY = y + ItemHeight / 2;

                if (item.SymbolBitmap != null)
                {
                    canvas.DrawBitmap(item.SymbolBitmap,
                        new SKRect(x, y, x + 20, y + 20));
                }
                else if (item.SymbolColor != null)
                {
                    var c = item.SymbolColor;
                    using var linePaint = new SKPaint
                    {
                        Color = new SKColor(
                            (byte)c.R, (byte)c.G, (byte)c.B, (byte)c.A),
                        StrokeWidth = item.SymbolLineWidth,
                        Style = SKPaintStyle.Stroke,
                        IsAntialias = true,
                    };
                    canvas.DrawLine(x, centerY, x + LineLength, centerY, linePaint);
                }

                canvas.DrawText(item.Label,
                    x + LineLength + LabelGap,
                    y + baselineY,
                    textPaint);

                y += ItemHeight;
            }
        }
    }
}
