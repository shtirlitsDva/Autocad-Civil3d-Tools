using DimensioneringV2.Themes;

using Mapsui.Styles;

using SkiaSharp;

using System;
using System.Linq;

namespace DimensioneringV2.Legend
{
    /// <summary>
    /// A gradient color bar with min/max labels.
    /// Compound element that renders the bar and its labels as one unit.
    /// The gradient colors are sampled from an <see cref="OklchGradient"/>.
    /// </summary>
    internal class GradientBar : LegendElement
    {
        public double Min { get; init; }
        public double Max { get; init; }
        public string? MinLabel { get; init; }
        public string? MaxLabel { get; init; }
        public float BarWidth { get; init; } = 35f;
        public float BarHeight { get; init; } = 100f;
        public float LabelGap { get; init; } = 10f;
        public OklchGradient? ColorGradient { get; init; }

        protected override SKSize MeasureCore(LegendRenderContext ctx)
        {
            var textPaint = ctx.GetTextPaint(14f, true, LegendTextAlign.Left, new Color(0, 0, 0));
            var metrics = ctx.GetMetrics(14f, true);

            float minLabelWidth = MinLabel != null ? textPaint.MeasureText(MinLabel) : 0;
            float maxLabelWidth = MaxLabel != null ? textPaint.MeasureText(MaxLabel) : 0;
            float textHeight = metrics.Descent - metrics.Ascent;

            float width = BarWidth + LabelGap + MathF.Max(minLabelWidth, maxLabelWidth);
            float height = MathF.Max(BarHeight, textHeight);

            return new SKSize(width, height);
        }

        protected override void DrawCore(SKCanvas canvas, SKRect bounds, LegendRenderContext ctx)
        {
            var textPaint = ctx.GetTextPaint(14f, true, LegendTextAlign.Left, new Color(0, 0, 0));
            var metrics = ctx.GetMetrics(14f, true);

            float barX = bounds.Left;
            float barY = bounds.Top;
            float labelX = barX + BarWidth + LabelGap;
            float textOffset = -metrics.Ascent;

            // Gradient shader — sample OKLCH gradient into RGB for SkiaSharp
            if (ColorGradient != null)
            {
                var (blendColors, blendPositions) = ColorGradient.Sample();
                var skColors = blendColors
                    .Select(c => new SKColor((byte)c.R, (byte)c.G, (byte)c.B, (byte)c.A))
                    .Reverse()
                    .ToArray();
                var positions = blendPositions.Select(p => (float)p).ToArray();

                using var gradientPaint = new SKPaint
                {
                    Shader = SKShader.CreateLinearGradient(
                        new SKPoint(barX, barY),
                        new SKPoint(barX, barY + BarHeight),
                        skColors,
                        positions,
                        SKShaderTileMode.Clamp),
                };
                canvas.DrawRect(
                    new SKRect(barX, barY, barX + BarWidth, barY + BarHeight),
                    gradientPaint);
            }

            // Max label at top
            if (MaxLabel != null)
            {
                canvas.DrawText(MaxLabel, labelX, barY + textOffset, textPaint);
            }

            // Min label at bottom
            if (MinLabel != null)
            {
                float minLabelBaselineY = barY + BarHeight - metrics.Descent;
                canvas.DrawText(MinLabel, labelX, minLabelBaselineY, textPaint);
            }
        }
    }
}
