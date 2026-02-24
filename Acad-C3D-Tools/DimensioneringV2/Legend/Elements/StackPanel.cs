using Mapsui.Styles;

using SkiaSharp;

using System.Collections.Generic;

namespace DimensioneringV2.Legend
{
    /// <summary>
    /// A container element that stacks children vertically or horizontally.
    /// Supports padding and an optional background color.
    /// CSS analogue: <c>display:flex</c> with <c>flex-direction</c>.
    /// </summary>
    internal class StackPanel : LegendElement
    {
        public LegendOrientation Orientation { get; init; } = LegendOrientation.Vertical;
        public List<LegendElement> Children { get; init; } = [];
        public Thickness Padding { get; init; }
        public Color? Background { get; init; }

        protected override SKSize MeasureCore(LegendRenderContext ctx)
        {
            float mainAxis = 0;
            float crossAxis = 0;

            foreach (var child in Children)
            {
                var childSize = child.Measure(ctx);

                if (Orientation == LegendOrientation.Vertical)
                {
                    mainAxis += childSize.Height;
                    if (childSize.Width > crossAxis) crossAxis = childSize.Width;
                }
                else
                {
                    mainAxis += childSize.Width;
                    if (childSize.Height > crossAxis) crossAxis = childSize.Height;
                }
            }

            float w, h;
            if (Orientation == LegendOrientation.Vertical)
            {
                w = crossAxis + Padding.Horizontal;
                h = mainAxis + Padding.Vertical;
            }
            else
            {
                w = mainAxis + Padding.Horizontal;
                h = crossAxis + Padding.Vertical;
            }

            return new SKSize(w, h);
        }

        protected override void DrawCore(SKCanvas canvas, SKRect bounds, LegendRenderContext ctx)
        {
            // Background
            if (Background != null)
            {
                var bg = Background;
                using var bgPaint = new SKPaint
                {
                    Color = new SKColor((byte)bg.R, (byte)bg.G, (byte)bg.B, (byte)bg.A),
                    Style = SKPaintStyle.Fill,
                };
                canvas.DrawRect(bounds, bgPaint);
            }

            // Content area inside padding
            float cx = bounds.Left + Padding.Left;
            float cy = bounds.Top + Padding.Top;
            float contentWidth = bounds.Width - Padding.Horizontal;
            float contentHeight = bounds.Height - Padding.Vertical;

            foreach (var child in Children)
            {
                var childSize = child.Measure(ctx);

                SKRect childBounds;
                if (Orientation == LegendOrientation.Vertical)
                {
                    // Full width available, height is measured
                    childBounds = new SKRect(cx, cy, cx + contentWidth, cy + childSize.Height);
                    cy += childSize.Height;
                }
                else
                {
                    // Full height available, width is measured
                    childBounds = new SKRect(cx, cy, cx + childSize.Width, cy + contentHeight);
                    cx += childSize.Width;
                }

                child.Draw(canvas, childBounds, ctx);
            }
        }
    }
}
