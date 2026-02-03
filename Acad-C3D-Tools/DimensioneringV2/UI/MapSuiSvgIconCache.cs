using Mapsui.Styles;

using SkiaSharp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DimensioneringV2.UI
{
    internal static class MapSuiSvgIconCache
    {
        private static readonly Dictionary<string, int> _normalBitmapIds = new();
        private static readonly Dictionary<string, int> _inactiveBitmapIds = new();
        private static bool _initialized = false;
        private static readonly object _lock = new();

        private static readonly Dictionary<string, string> _typeToSvgMap = new()
        {
            { "El", "El.svg" },
            { "Naturgas", "Naturgas.svg" },
            { "Varmepumpe", "Varmepumpe.svg" },
            { "Fast brændsel", "Fast brændsel.svg" },
            { "Olie", "Olie.svg" },
            { "Fjernvarme", "Fjernvarme.svg" },
            { "Andet", "Ingen.svg" },
            { "Ingen", "Ingen.svg" },
            { "UDGÅR", "Ingen.svg" }
        };

        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized) return;

                DimensioneringV2.Utils.prtDbg("MapSuiSvgIconCache: Initializing...");

                foreach (var (heatingType, svgFile) in _typeToSvgMap)
                {
                    var drawingImage = EmbeddedSvgLoader.LoadSvg(svgFile);
                    if (drawingImage == null)
                    {
                        DimensioneringV2.Utils.prtDbg($"MapSuiSvgIconCache: Failed to load {svgFile}");
                        continue;
                    }

                    var pngBytes = ConvertToPngBytes(drawingImage, 48);
                    if (pngBytes != null)
                    {
                        var normalStream = new MemoryStream(pngBytes);
                        var normalId = BitmapRegistry.Instance.Register(normalStream);
                        _normalBitmapIds[heatingType] = normalId;
                        DimensioneringV2.Utils.prtDbg($"MapSuiSvgIconCache: Registered {heatingType} -> ID {normalId}");

                        var inactiveBytes = CreateDimmedPngBytes(pngBytes, 0.35f);
                        var inactiveStream = new MemoryStream(inactiveBytes);
                        var inactiveId = BitmapRegistry.Instance.Register(inactiveStream);
                        _inactiveBitmapIds[heatingType] = inactiveId;
                    }
                    else
                    {
                        DimensioneringV2.Utils.prtDbg($"MapSuiSvgIconCache: Failed to convert {svgFile} to PNG");
                    }
                }

                DimensioneringV2.Utils.prtDbg($"MapSuiSvgIconCache: Done. Registered {_normalBitmapIds.Count} icons");
                _initialized = true;
            }
        }

        public static int GetBitmapId(string heatingType)
        {
            if (!_initialized) Initialize();
            return _normalBitmapIds.TryGetValue(heatingType, out var id) ? id : -1;
        }

        public static int GetInactiveBitmapId(string heatingType)
        {
            if (!_initialized) Initialize();
            return _inactiveBitmapIds.TryGetValue(heatingType, out var id) ? id : -1;
        }

        private static byte[]? ConvertToPngBytes(DrawingImage image, int targetSize)
        {
            try
            {
                var rtb = new RenderTargetBitmap(
                    targetSize, targetSize, 96, 96, PixelFormats.Pbgra32);

                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.DrawImage(image, new Rect(0, 0, targetSize, targetSize));
                }
                rtb.Render(dv);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using var ms = new MemoryStream();
                encoder.Save(ms);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                DimensioneringV2.Utils.prtDbg($"Error converting DrawingImage to PNG: {ex.Message}");
                return null;
            }
        }

        private static byte[] CreateDimmedPngBytes(byte[] originalPngBytes, float opacity)
        {
            using var original = SKBitmap.Decode(originalPngBytes);
            using var dimmed = new SKBitmap(original.Width, original.Height);

            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    var pixel = original.GetPixel(x, y);
                    var newAlpha = (byte)(pixel.Alpha * opacity);
                    dimmed.SetPixel(x, y, new SKColor(pixel.Red, pixel.Green, pixel.Blue, newAlpha));
                }
            }

            using var skImage = SKImage.FromBitmap(dimmed);
            using var data = skImage.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
    }
}
