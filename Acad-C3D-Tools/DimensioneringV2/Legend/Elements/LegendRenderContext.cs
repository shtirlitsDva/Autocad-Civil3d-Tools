using Mapsui.Styles;

using SkiaSharp;

using System;
using System.Collections.Generic;

namespace DimensioneringV2.Legend
{
    /// <summary>
    /// Provides cached SKPaint objects and font metrics for legend rendering.
    /// Created once per Draw call and disposed afterwards.
    /// Elements call <see cref="GetTextPaint"/> and <see cref="GetMetrics"/>
    /// instead of allocating their own paints.
    /// </summary>
    internal class LegendRenderContext : IDisposable
    {
        private readonly Dictionary<(float size, bool bold, LegendTextAlign align, uint color), SKPaint> _textPaints = new();
        private readonly Dictionary<(float size, bool bold), SKFontMetrics> _metricsCache = new();

        public SKPaint GetTextPaint(float fontSize, bool bold, LegendTextAlign align, Color color)
        {
            uint colorKey = (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
            var key = (fontSize, bold, align, colorKey);

            if (!_textPaints.TryGetValue(key, out var paint))
            {
                paint = new SKPaint
                {
                    Color = new SKColor((byte)color.R, (byte)color.G, (byte)color.B, (byte)color.A),
                    TextSize = fontSize,
                    IsAntialias = true,
                    Typeface = SKTypeface.FromFamilyName("Arial",
                        bold ? SKFontStyle.Bold : SKFontStyle.Normal),
                    TextAlign = align switch
                    {
                        LegendTextAlign.Center => SKTextAlign.Center,
                        LegendTextAlign.Right => SKTextAlign.Right,
                        _ => SKTextAlign.Left,
                    },
                };
                _textPaints[key] = paint;
            }
            return paint;
        }

        public SKFontMetrics GetMetrics(float fontSize, bool bold)
        {
            var key = (fontSize, bold);
            if (!_metricsCache.TryGetValue(key, out var metrics))
            {
                using var tempPaint = new SKPaint
                {
                    TextSize = fontSize,
                    Typeface = SKTypeface.FromFamilyName("Arial",
                        bold ? SKFontStyle.Bold : SKFontStyle.Normal),
                };
                tempPaint.GetFontMetrics(out metrics);
                _metricsCache[key] = metrics;
            }
            return metrics;
        }

        public void Dispose()
        {
            foreach (var paint in _textPaints.Values)
                paint.Dispose();
            _textPaints.Clear();
            _metricsCache.Clear();
        }
    }
}
