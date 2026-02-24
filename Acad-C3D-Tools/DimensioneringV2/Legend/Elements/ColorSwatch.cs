using Mapsui.Styles;

using SkiaSharp;

namespace DimensioneringV2.Legend
{
    /// <summary>
    /// A single colored horizontal line — used as a legend symbol.
    /// CSS analogue: <c>&lt;div&gt;</c> with a background color and fixed dimensions.
    /// </summary>
    internal class ColorSwatch : LegendElement
    {
        public Color SwatchColor { get; init; } = new Color(0, 0, 0);
        public float LineWidth { get; init; } = 3f;
        public float Length { get; init; } = 30f;

        protected override SKSize MeasureCore(LegendRenderContext ctx)
            => new(Length, LineWidth);

        protected override void DrawCore(SKCanvas canvas, SKRect bounds, LegendRenderContext ctx)
        {
            using var paint = new SKPaint
            {
                Color = new SKColor(
                    (byte)SwatchColor.R,
                    (byte)SwatchColor.G,
                    (byte)SwatchColor.B,
                    (byte)SwatchColor.A),
                StrokeWidth = LineWidth,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true,
            };

            float centerY = bounds.Top + bounds.Height / 2;
            canvas.DrawLine(bounds.Left, centerY, bounds.Left + Length, centerY, paint);
        }
    }
}
