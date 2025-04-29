using IntersectUtilities.UtilsCommon;

using Mapsui;
using Mapsui.Rendering.Skia.SkiaWidgets;
using Mapsui.Widgets;

using SkiaSharp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.TextFormatting;

namespace DimensioneringV2.Legend
{
    internal class LegendWidgetSkiaRenderer : ISkiaWidgetRenderer
    {
        public void Draw(SKCanvas canvas, Viewport viewport, IWidget widget, float layerOpacity)
        {
            if (widget is not LegendWidget lw) return;
            if (!lw.Enabled) return;

            ILegendData? ld = lw.LegendData;
            if (ld == null) return;

            switch (ld.LegendType)
            {
                case LegendType.None:
                    return;
                case LegendType.Categorical:
                    {
                        //Settings
                        int leftMargin = 10;
                        int rightMargin = 10;
                        int topMargin = 5;
                        int bottomMargin = 5;

                        int legendLineLength = 30;

                        int distanceBtwLineAndText = 10;

                        float textSize = 14;

                        var legendItems = ld.Items.Where(x => x.Label.IsNotNoE()).ToList();

                        float x = lw.MarginX;
                        float y = lw.MarginY;

                        using var backgroundPaint = new SKPaint
                        {
                            Color = new SKColor(255, 255, 255, 200), // semi-transparent white background
                            Style = SKPaintStyle.Fill
                        };

                        using var textPaint = new SKPaint
                        {
                            Color = SKColors.Black,
                            TextSize = textSize,
                            IsAntialias = true,
                            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
                            TextAlign = SKTextAlign.Left,
                        };
                        SKFontMetrics metrics;
                        textPaint.GetFontMetrics(out metrics);

                        // Calculate background size based on items
                        int itemHeight = 18;
                        int width =
                            //Text width
                            (int)legendItems.Select(x => textPaint.MeasureText(x.Label)).Max() +
                            //Left margin, Line length, Between space, Right margin
                            leftMargin + legendLineLength + distanceBtwLineAndText + rightMargin;
                        int height = legendItems.Count * itemHeight + topMargin + bottomMargin;

                        float textHeight = metrics.Descent - metrics.Ascent;
                        float baselineY = (itemHeight - textHeight) / 2 - metrics.Ascent;

                        canvas.DrawRect(x - 10, y - 10, width, height, backgroundPaint);

                        //Adjust text start
                        y -= 5;

#if DEBUG
                        canvas.DrawText(
                            $"x:{x} y:{y}, width:{width}, height:{height}",
                            x, y + 250, textPaint);
#endif

                        foreach (LegendItem item in legendItems)
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
                                canvas.DrawLine(x, centerY, x + legendLineLength, centerY, linePaint);
                            }

                            canvas.DrawText(item.Label, x + legendLineLength + distanceBtwLineAndText, y + baselineY, textPaint);

                            y += itemHeight;
                        }
                    }
                    break;
                case LegendType.Gradient:
                    {

                    }
                    break;
                default:
                    return;
            }            
        }
    }
}