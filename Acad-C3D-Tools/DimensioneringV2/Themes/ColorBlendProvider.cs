using Mapsui.Styles;

using System;
using System.Linq;

namespace DimensioneringV2.Themes
{
    internal static class ColorBlendProvider
    {
        internal static OklchGradient Standard => Wide;

        /// <summary>
        /// 7-stop OKLCH gradient: blue → cyan → green → yellow-green → yellow → orange → red.
        /// Lightness kept in 58–83% range; hue sweeps around the wheel to avoid muddy grays.
        /// </summary>
        internal static OklchGradient Wide => new(
            new OklchColor(0.62, 0.18, 260), // Cool blue
            new OklchColor(0.68, 0.14, 215), // Cyan
            new OklchColor(0.74, 0.17, 155), // Green
            new OklchColor(0.82, 0.20, 115), // Yellow-green
            new OklchColor(0.87, 0.19, 90),  // Warning yellow
            new OklchColor(0.76, 0.20, 55),  // Orange
            new OklchColor(0.65, 0.24, 28)   // Danger red
        );

        /// <summary>
        /// 5-stop OKLCH gradient: green → yellow-green → yellow → orange → red.
        /// Compact "traffic light" progression for mid-to-high severity ranges.
        /// </summary>
        internal static OklchGradient GreenYellowRed => new(
            new OklchColor(0.72, 0.17, 148), // Green
            new OklchColor(0.82, 0.20, 118), // Yellow-green
            new OklchColor(0.88, 0.19, 92),  // Yellow
            new OklchColor(0.76, 0.20, 55),  // Orange
            new OklchColor(0.63, 0.24, 27)   // Red
        );
    }

    /// <summary>
    /// A gradient defined by OKLCH color stops with perceptually uniform interpolation.
    /// Replaces Mapsui's ColorBlend to avoid muddy RGB-space interpolation.
    /// </summary>
    internal class OklchGradient
    {
        private readonly OklchColor[] _stops;

        public OklchGradient(params OklchColor[] stops)
        {
            _stops = stops;
        }

        /// <summary>
        /// Returns the interpolated color at position <paramref name="t"/> (0–1)
        /// via OKLCH-space interpolation, converted to Mapsui Color.
        /// </summary>
        public Color GetColor(double t)
        {
            t = Math.Clamp(t, 0, 1);
            return Interpolate(t).ToMapsuiColor();
        }

        /// <summary>
        /// Pre-samples the gradient into evenly-spaced RGB colors and positions.
        /// Use this for SkiaSharp gradient shaders that require Color[]/float[] arrays.
        /// </summary>
        public (Color[] Colors, double[] Positions) Sample(int count = 32)
        {
            var colors = new Color[count];
            var positions = new double[count];

            for (int i = 0; i < count; i++)
            {
                double t = (double)i / (count - 1);
                positions[i] = t;
                colors[i] = Interpolate(t).ToMapsuiColor();
            }

            return (colors, positions);
        }

        private OklchColor Interpolate(double t)
        {
            double segT = t * (_stops.Length - 1);
            int idx = Math.Min((int)segT, _stops.Length - 2);
            double localT = segT - idx;

            var a = _stops[idx];
            var b = _stops[idx + 1];

            double l = a.L + (b.L - a.L) * localT;
            double c = a.C + (b.C - a.C) * localT;

            // Shortest-path hue interpolation around the 360° wheel
            double dh = b.H - a.H;
            if (dh > 180) dh -= 360;
            if (dh < -180) dh += 360;
            double h = (a.H + dh * localT) % 360;
            if (h < 0) h += 360;

            return new OklchColor(l, c, h);
        }
    }

    /// <summary>
    /// A color in the OKLCH perceptual color space.
    /// Converts to sRGB via the OKLab → LMS → linear sRGB → sRGB pipeline.
    /// </summary>
    internal readonly struct OklchColor
    {
        public readonly double L, C, H;

        /// <param name="l">Lightness 0–1</param>
        /// <param name="c">Chroma 0–~0.4</param>
        /// <param name="h">Hue 0–360°</param>
        public OklchColor(double l, double c, double h)
        {
            L = l; C = c; H = h;
        }

        public Color ToMapsuiColor()
        {
            // OKLCH → OKLab (polar → cartesian)
            double hRad = H * Math.PI / 180.0;
            double a = C * Math.Cos(hRad);
            double b = C * Math.Sin(hRad);

            // OKLab → LMS (cube-root domain)
            double l_ = L + 0.3963377774 * a + 0.2158037573 * b;
            double m_ = L - 0.1055613458 * a - 0.0638541728 * b;
            double s_ = L - 0.0894841775 * a - 1.2914855480 * b;

            // Undo cube root → linear LMS
            double l = l_ * l_ * l_;
            double m = m_ * m_ * m_;
            double s = s_ * s_ * s_;

            // Linear LMS → linear sRGB
            double r = +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
            double g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
            double bl = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;

            return new Color(
                (int)Math.Round(Math.Clamp(LinearToSrgb(r) * 255.0, 0, 255)),
                (int)Math.Round(Math.Clamp(LinearToSrgb(g) * 255.0, 0, 255)),
                (int)Math.Round(Math.Clamp(LinearToSrgb(bl) * 255.0, 0, 255)));
        }

        private static double LinearToSrgb(double x)
        {
            if (x <= 0) return 0;
            return x >= 0.0031308
                ? 1.055 * Math.Pow(x, 1.0 / 2.4) - 0.055
                : 12.92 * x;
        }
    }
}
