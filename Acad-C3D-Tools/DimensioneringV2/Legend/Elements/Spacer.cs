using SkiaSharp;

namespace DimensioneringV2.Legend
{
    /// <summary>
    /// An explicit spacing element — an empty rectangle of fixed size.
    /// CSS analogue: <c>&lt;div style="width:Xpx; height:Ypx"&gt;</c>
    /// </summary>
    internal class Spacer : LegendElement
    {
        public float Width { get; init; }
        public float Height { get; init; }

        protected override SKSize MeasureCore(LegendRenderContext ctx)
            => new(Width, Height);

        protected override void DrawCore(SKCanvas canvas, SKRect bounds, LegendRenderContext ctx)
        {
            // No-op — spacers are invisible
        }
    }
}
