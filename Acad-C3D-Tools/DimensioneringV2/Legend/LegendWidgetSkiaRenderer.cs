using Mapsui;
using Mapsui.Rendering.Skia.SkiaWidgets;
using Mapsui.Widgets;

using SkiaSharp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Legend
{
    internal class LegendWidgetSkiaRenderer : ISkiaWidgetRenderer
    {
        public void Draw(SKCanvas canvas, Viewport viewport, IWidget widget, float layerOpacity)
        {
            if (widget is not LegendWidget legendWidget) return;
            if (!legendWidget.Enabled) return;

            float x = legendWidget.MarginX;
            float y = legendWidget.MarginY;

            using var backgroundPaint = new SKPaint
            {
                Color = new SKColor(255, 255, 255, 200), // semi-transparent white background
                Style = SKPaintStyle.Fill
            };

            // Calculate background size based on items
            int itemHeight = 24;
            int width = 200;
            int height = legendWidget.Items.Count * itemHeight + 10;

            canvas.DrawRect(x - 10, y - 10, width, height, backgroundPaint);

            foreach (LegendItem item in legendWidget.Items)
            {
                float symbolLineWidth = item.SymbolLineWidth;
                float centerY = y + itemHeight / 2;

                if (item.SymbolBitmap != null)
                {
                    canvas.DrawBitmap(item.SymbolBitmap, new SKRect(x, y, x + 20, y + 20));
                }
                else if (item.SymbolColor != null)
                {
                    var mapsuiColor = item.SymbolColor;
                    using var linePaint = new SKPaint
                    {
                        Color = new SKColor(
                            (byte)mapsuiColor.R,
                            (byte)mapsuiColor.G,
                            (byte)mapsuiColor.B,
                            (byte)mapsuiColor.A),
                        StrokeWidth = symbolLineWidth,
                        Style = SKPaintStyle.Stroke,
                        IsAntialias = true
                    };

                    // Draw a horizontal line centered vertically
                    canvas.DrawLine(x, centerY, x + 30, centerY, linePaint);
                }

                using var textPaint = new SKPaint
                {
                    Color = SKColors.Black,
                    TextSize = 14,
                    IsAntialias = true
                };
                canvas.DrawText(item.Label, x + 40, y + 6, textPaint);

                y += itemHeight;
            }
        }
    }
}