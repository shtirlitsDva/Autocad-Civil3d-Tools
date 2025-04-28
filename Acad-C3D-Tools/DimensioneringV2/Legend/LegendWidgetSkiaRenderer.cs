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
            int itemHeight = 30;
            int width = 200;
            int height = legendWidget.Items.Count * itemHeight + 10;

            canvas.DrawRect(x - 10, y - 10, width, height, backgroundPaint);

            foreach (var item in legendWidget.Items)
            {
                if (item.SymbolBitmap != null)
                {
                    canvas.DrawBitmap(item.SymbolBitmap, new SKRect(x, y, x + 20, y + 20));
                }
                else if (item.SymbolColor != null)
                {
                    var mapsuiColor = item.SymbolColor;

                    using var symbolPaint = new SKPaint
                    {
                        Color = new SKColor(
                            (byte)mapsuiColor.R,
                            (byte)mapsuiColor.G, 
                            (byte)mapsuiColor.B, 
                            (byte)mapsuiColor.A),
                        Style = SKPaintStyle.Fill
                    };
                    canvas.DrawRect(x, y, x + 20, y + 20, symbolPaint);
                }

                using var textPaint = new SKPaint
                {
                    Color = SKColors.Black,
                    TextSize = 16,
                    IsAntialias = true
                };
                canvas.DrawText(item.Label, x + 30, y + 17, textPaint);

                y += itemHeight;
            }
        }
    }
}