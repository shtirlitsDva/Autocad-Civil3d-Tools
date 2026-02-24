using SkiaSharp;

namespace DimensioneringV2.Legend
{
    internal record struct Thickness(float Left, float Top, float Right, float Bottom)
    {
        public Thickness(float uniform) : this(uniform, uniform, uniform, uniform) { }
        public Thickness(float horizontal, float vertical) : this(horizontal, vertical, horizontal, vertical) { }
        public float Horizontal => Left + Right;
        public float Vertical => Top + Bottom;
    }

    internal enum LegendTextAlign { Left, Center, Right }

    internal enum LegendOrientation { Vertical, Horizontal }

    /// <summary>
    /// Abstract base for all legend elements.
    /// Every element is a rectangle with optional margin.
    /// Subclasses implement <see cref="MeasureCore"/> and <see cref="DrawCore"/> —
    /// the base class handles margin arithmetic automatically.
    /// </summary>
    internal abstract class LegendElement
    {
        public Thickness Margin { get; init; }

        /// <summary>
        /// Returns the total size of this element including its margin.
        /// </summary>
        internal SKSize Measure(LegendRenderContext ctx)
        {
            var core = MeasureCore(ctx);
            return new SKSize(
                core.Width + Margin.Horizontal,
                core.Height + Margin.Vertical);
        }

        /// <summary>
        /// Draws this element within the given bounds (which include the margin).
        /// The margin is subtracted before calling <see cref="DrawCore"/>.
        /// </summary>
        internal void Draw(SKCanvas canvas, SKRect bounds, LegendRenderContext ctx)
        {
            var inner = new SKRect(
                bounds.Left + Margin.Left,
                bounds.Top + Margin.Top,
                bounds.Right - Margin.Right,
                bounds.Bottom - Margin.Bottom);
            DrawCore(canvas, inner, ctx);
        }

        /// <summary>
        /// Measures the content size (excluding margin).
        /// </summary>
        protected abstract SKSize MeasureCore(LegendRenderContext ctx);

        /// <summary>
        /// Draws the content within the given bounds (margin already subtracted).
        /// </summary>
        protected abstract void DrawCore(SKCanvas canvas, SKRect bounds, LegendRenderContext ctx);
    }
}
