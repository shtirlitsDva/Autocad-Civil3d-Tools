using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Media;

namespace DimensioneringV2.UI.Infrastructure
{
    public static class EmbeddedSvgLoader
    {
        private static readonly Dictionary<string, DrawingImage> _cache = new();
        private static readonly WpfDrawingSettings _settings;

        static EmbeddedSvgLoader()
        {
            _settings = new WpfDrawingSettings
            {
                IncludeRuntime = true,
                TextAsGeometry = false
            };
        }

        public static DrawingImage? LoadSvg(string resourceName)
        {
            if (_cache.TryGetValue(resourceName, out var cached))
                return cached;

            var assembly = Assembly.GetExecutingAssembly();
            var fullResourceName = $"DimensioneringV2.Resources.Icons.{resourceName}";

            using var stream = assembly.GetManifestResourceStream(fullResourceName);
            if (stream == null)
            {
                System.Diagnostics.Debug.WriteLine($"Could not find embedded resource: {fullResourceName}");
                return null;
            }

            try
            {
                using var reader = new FileSvgReader(_settings);
                var drawing = reader.Read(stream);

                if (drawing != null)
                {
                    var image = new DrawingImage(drawing);
                    image.Freeze();
                    _cache[resourceName] = image;
                    return image;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading SVG {resourceName}: {ex.Message}");
            }

            return null;
        }
    }
}
