using DimensioneringV2.Themes;

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

            //Settings
            int leftMargin = 10;
            int rightMargin = 10;
            int topMargin = 5;
            int bottomMargin = 5;

            int legendLineLength = 30;

            int distanceBtwLineAndText = 10;

            float textSize = 14;
            float titleSize = 16;

            float x = lw.MarginX;
            float y = lw.MarginY;

            using var backgroundPaint = new SKPaint
            {
                Color = new SKColor(255, 255, 255, 200), // semi-transparent white background
                Style = SKPaintStyle.Fill
            };

            using var textPaintLegendText = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = textSize,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
                TextAlign = SKTextAlign.Left,
            };
            SKFontMetrics metricsLegendText;
            textPaintLegendText.GetFontMetrics(out metricsLegendText);

            using var textPaintTitleText = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = titleSize,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
                TextAlign = SKTextAlign.Center,
            };
            SKFontMetrics metricsTitleText;
            textPaintTitleText.GetFontMetrics(out metricsTitleText);

            switch (ld.LegendType)
            {
                case LegendType.None:
                    return;
                case LegendType.Categorical:
                    {
                        var legendItems = ld.Items.Where(x => x.Label.IsNotNoE()).ToList();

                        const int itemHeight = 18;
                        const int titleBuffer = 4;

                        var (titleHeight, titleMaxWidth) = MeasureLegendTitle(ld.LegendTitle, textPaintTitleText, metricsTitleText);

                        int contentWidth = (int)legendItems.Select(x => textPaintLegendText.MeasureText(x.Label)).Max();
                        int width = Math.Max(
                            (int)titleMaxWidth,
                            contentWidth + leftMargin + legendLineLength + distanceBtwLineAndText + rightMargin);

                        int height = legendItems.Count * itemHeight
                            + topMargin
                            + (int)titleHeight
                            + titleBuffer
                            + bottomMargin;

                        float textHeight = metricsLegendText.Descent - metricsLegendText.Ascent;
                        float baselineY = (itemHeight - textHeight) / 2 - metricsLegendText.Ascent;

                        float outerX = x - 10;
                        float outerY = y - 10;
                        float titleXCenter = outerX + leftMargin + (width - leftMargin - rightMargin) / 2;
                        float titleYStart = outerY + topMargin;

                        canvas.DrawRect(outerX, outerY, width, height, backgroundPaint);
                        DrawLegendTitle(canvas, ld.LegendTitle, titleXCenter, titleYStart, textPaintTitleText, metricsTitleText);

                        // Advance y to the start of the first legend item
                        y = titleYStart + titleHeight + titleBuffer;

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

                                canvas.DrawLine(x, centerY, x + legendLineLength, centerY, linePaint);
                            }

                            canvas.DrawText(item.Label, x + legendLineLength + distanceBtwLineAndText, y + baselineY, textPaintLegendText);
                            y += itemHeight;
                        }
                    }
                    break;
                case LegendType.Gradient:
                    {
                        const int barWidth = 35;
                        const int barHeight = 100;
                        const int titleBuffer = 4;

                        bool c = ld.Min % 1 != 0 || ld.Max % 1 != 0;
                        var minLabel = c ? ld.Min.ToString("F2") : ld.Min.ToString("F0");
                        var maxLabel = c ? ld.Max.ToString("F2") : ld.Max.ToString("F0");

                        // Gradient shader
                        var blend = ColorBlendProvider.Standard;
                        var skColors = blend.Colors.Select(
                            c => new SKColor((byte)c.R, (byte)c.G, (byte)c.B, (byte)c.A))
                            .Reverse()
                            .ToArray();
                        var positions = blend.Positions.Select(p => (float)p).ToArray();                        

                        // Measure text
                        float minLabelWidth = textPaintLegendText.MeasureText(minLabel);
                        float maxLabelWidth = textPaintLegendText.MeasureText(maxLabel);
                        float textHeight = metricsLegendText.Descent - metricsLegendText.Ascent;
                        float textOffset = -metricsLegendText.Ascent;

                        // Measure title
                        var (titleHeight, titleMaxWidth) = MeasureLegendTitle(ld.LegendTitle, textPaintTitleText, metricsTitleText);

                        float legendWidth = leftMargin + barWidth + distanceBtwLineAndText + MathF.Max(minLabelWidth, maxLabelWidth) + rightMargin;
                        //The last -bottomMargin is a hack fix
                        float legendHeight = topMargin + titleHeight + titleBuffer + barHeight + textHeight - bottomMargin;// + bottomMargin;

                        float outerX = x - 10;
                        float outerY = y - 10;
                        float titleXCenter = outerX + leftMargin + (legendWidth - leftMargin - rightMargin) / 2;
                        float titleYStart = outerY + topMargin;

                        canvas.DrawRect(outerX, outerY, legendWidth, legendHeight, backgroundPaint);

                        DrawLegendTitle(canvas, ld.LegendTitle, titleXCenter, titleYStart, textPaintTitleText, metricsTitleText);

                        float barX = outerX + leftMargin;
                        float labelX = barX + barWidth + distanceBtwLineAndText;
                        float barY = titleYStart + titleHeight + titleBuffer;
                        using var gradientPaint = new SKPaint
                        {
                            Shader = SKShader.CreateLinearGradient(
                                new SKPoint(barX, barY),
                                new SKPoint(barX, barY + barHeight),
                                skColors,
                                positions,
                                SKShaderTileMode.Clamp)
                        };
                        canvas.DrawRect(new SKRect(barX, barY, barX + barWidth, barY + barHeight), gradientPaint);

                        // Labels
                        canvas.DrawText(maxLabel, labelX, barY + textOffset, textPaintLegendText);

                        float minLabelBaselineY = barY + barHeight - metricsLegendText.Descent;
                        canvas.DrawText(minLabel, labelX, minLabelBaselineY, textPaintLegendText);
                    }
                    break;
                default:
                    return;
            }
        }

        private (float totalHeight, float maxLineWidth) MeasureLegendTitle(string title, SKPaint paint, SKFontMetrics metrics)
        {
            if (string.IsNullOrWhiteSpace(title)) return (0, 0);

            var lines = title.Split('\n');
            float lineHeight = metrics.Descent - metrics.Ascent;
            float maxWidth = lines.Max(line => paint.MeasureText(line));

            return (lines.Length * lineHeight, maxWidth);
        }

        private void DrawLegendTitle(SKCanvas canvas, string title, float xCenter, float yStart, SKPaint paint, SKFontMetrics metrics)
        {
            if (string.IsNullOrWhiteSpace(title)) return;

            var lines = title.Split('\n');
            float lineHeight = metrics.Descent - metrics.Ascent;

            for (int i = 0; i < lines.Length; i++)
            {
                float lineY = yStart + i * lineHeight - metrics.Ascent;
                canvas.DrawText(lines[i], xCenter, lineY, paint);
            }
        }
    }
}