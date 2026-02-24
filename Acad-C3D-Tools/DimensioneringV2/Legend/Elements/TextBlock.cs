using Mapsui.Styles;

using SkiaSharp;

using System;
using System.Linq;

namespace DimensioneringV2.Legend
{
    /// <summary>
    /// A styled text element with multi-line support.
    /// Lines are split on <c>\n</c> characters.
    /// CSS analogue: <c>&lt;div&gt;</c> with font properties.
    /// </summary>
    internal class TextBlock : LegendElement
    {
        public string Text { get; init; } = "";
        public float FontSize { get; init; } = 14f;
        public bool Bold { get; init; } = true;
        public LegendTextAlign Align { get; init; } = LegendTextAlign.Left;
        public Color Color { get; init; } = new Color(0, 0, 0);

        protected override SKSize MeasureCore(LegendRenderContext ctx)
        {
            if (string.IsNullOrWhiteSpace(Text)) return SKSize.Empty;

            var paint = ctx.GetTextPaint(FontSize, Bold, LegendTextAlign.Left, Color);
            var metrics = ctx.GetMetrics(FontSize, Bold);

            var lines = Text.Split('\n');
            float lineHeight = metrics.Descent - metrics.Ascent;
            float maxWidth = lines.Max(line => paint.MeasureText(line));

            return new SKSize(maxWidth, lines.Length * lineHeight);
        }

        protected override void DrawCore(SKCanvas canvas, SKRect bounds, LegendRenderContext ctx)
        {
            if (string.IsNullOrWhiteSpace(Text)) return;

            var paint = ctx.GetTextPaint(FontSize, Bold, Align, Color);
            var metrics = ctx.GetMetrics(FontSize, Bold);

            var lines = Text.Split('\n');
            float lineHeight = metrics.Descent - metrics.Ascent;

            for (int i = 0; i < lines.Length; i++)
            {
                float lineY = bounds.Top + i * lineHeight - metrics.Ascent;
                float lineX = Align switch
                {
                    LegendTextAlign.Center => bounds.Left + bounds.Width / 2,
                    LegendTextAlign.Right => bounds.Right,
                    _ => bounds.Left,
                };
                canvas.DrawText(lines[i], lineX, lineY, paint);
            }
        }
    }
}
