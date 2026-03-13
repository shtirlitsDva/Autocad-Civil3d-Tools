using BruTile;
using BruTile.Cache;
using BruTile.Web;

using Mapsui;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;

using SkiaSharp;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal static class BaseMapLayerFactory
    {
        public const string BaseMapLayerName = "BaseMap";

        private static readonly Color WhiteBackColor = Color.White;
        private static readonly Color DarkBackColor = Color.Black;

        public static void ApplyBaseMap(Map map, BaseMapType type)
        {
            RemoveBaseMapLayers(map);

            map.BackColor = type == BaseMapType.Skaermkort
                ? WhiteBackColor
                : DarkBackColor;

            switch (type)
            {
                case BaseMapType.Skaermkort:
                    var skLayer = CreateSkaermkortLayer(
                        servicePath: "topo_skaermkort_wmts",
                        layerName: "topo_skaermkort");
                    if (skLayer != null) map.Layers.Insert(0, skLayer);
                    break;

                case BaseMapType.SkaermkortDaempet:
                    var skdLayer = CreateSkaermkortLayer(
                        servicePath: "topo_skaermkort_daempet",
                        layerName: "topo_skaermkort_daempet");
                    if (skdLayer != null) map.Layers.Insert(0, skdLayer);
                    break;

                case BaseMapType.SkaermkortDark:
                    var darkLayer = CreateSkaermkortLayer(
                        servicePath: "topo_skaermkort_daempet",
                        layerName: "topo_skaermkort_daempet",
                        invertColors: true);
                    if (darkLayer != null) map.Layers.Insert(0, darkLayer);
                    break;

                case BaseMapType.Ortofoto:
                    var ortoLayer = CreateOrtofotoLayer();
                    if (ortoLayer != null) map.Layers.Insert(0, ortoLayer);
                    break;

                case BaseMapType.Off:
                    break;
            }
        }

        public static void RemoveBaseMapLayers(Map map)
        {
            map.Layers.Remove(l => l.Name == BaseMapLayerName);
        }

        #region Custom EPSG:25832 Tile Schema (View1 matrix)
        private static TileSchema CreateView1TileSchema(string format = "image/jpeg")
        {
            double originX = 120000.0;
            double originY = 6500000.0;

            double[] resolutions =
            {
                1638.4, 819.2, 409.6, 204.8, 102.4, 51.2, 25.6, 12.8,
                6.4, 3.2, 1.6, 0.8, 0.4, 0.2, 0.1, 0.05
            };

            var schema = new TileSchema
            {
                OriginX = originX,
                OriginY = originY,
                Srs = "EPSG:25832",
                Format = format,
                YAxis = YAxis.OSM,
                Extent = new Extent(120000, 5900000, 1000000, 6500000),
            };

            for (int i = 0; i < resolutions.Length; i++)
            {
                schema.Resolutions[i] = new Resolution(
                    i, resolutions[i], 256, 256);
            }

            return schema;
        }
        #endregion

        #region Tile layer creation
        private static TileLayer? CreateSkaermkortLayer(
            string servicePath, string layerName, bool invertColors = false)
        {
            try
            {
                string apiKey = IntersectUtilities.UtilsCommon.Infrastructure.DATAFORDELER_APIKEY;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    DimensioneringV2.Utils.prtDbg(
                        $"Skærmkort unavailable: DATAFORDELER_APIKEY not configured in Infra.json");
                    return null;
                }

                var schema = CreateView1TileSchema("image/jpeg");

                var url =
                    $"https://wmts.datafordeler.dk/DKskaermkort/{servicePath}/1.0.0/WMTS"
                    + $"?apikey={apiKey}"
                    + "&SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0"
                    + $"&LAYER={layerName}"
                    + "&STYLE=default&FORMAT=image/jpeg"
                    + "&TILEMATRIXSET=View1"
                    + "&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}";

                var innerSource = new HttpTileSource(
                    schema,
                    url,
                    name: $"Skærmkort ({layerName})",
                    attribution: new BruTile.Attribution(
                        "© SDFI / Datafordeler",
                        "https://datafordeler.dk"));

                if (invertColors)
                {
                    var invertedSource = new InvertingTileSource(innerSource);
                    return new TileLayer(invertedSource) { Name = BaseMapLayerName };
                }

                return new TileLayer(innerSource) { Name = BaseMapLayerName };
            }
            catch (Exception ex)
            {
                DimensioneringV2.Utils.prtDbg($"Skærmkort layer creation failed: {ex.Message}");
                return null;
            }
        }

        private static TileLayer? CreateOrtofotoLayer()
        {
            try
            {
                string apiKey = IntersectUtilities.UtilsCommon.Infrastructure.DATAFORDELER_APIKEY;

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    DimensioneringV2.Utils.prtDbg(
                        "Ortofoto unavailable: DATAFORDELER_APIKEY not configured in Infra.json");
                    return null;
                }

                var schema = CreateView1TileSchema();

                var url =
                    "https://wmts.datafordeler.dk/GeoDanmarkOrto/orto_foraar_wmts/1.0.0/WMTS"
                    + $"?apikey={apiKey}"
                    + "&SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0"
                    + "&LAYER=orto_foraar_wmts"
                    + "&STYLE=default&FORMAT=image/jpeg"
                    + "&TILEMATRIXSET=KortforsyningTilingDK"
                    + "&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}";

                var source = new HttpTileSource(
                    schema,
                    url,
                    name: "Datafordeler Ortofoto (25832)",
                    attribution: new BruTile.Attribution(
                        "© SDFI / Datafordeler",
                        "https://datafordeler.dk"));

                return new TileLayer(source) { Name = BaseMapLayerName };
            }
            catch (Exception ex)
            {
                DimensioneringV2.Utils.prtDbg($"Ortofoto layer creation failed: {ex.Message}");
                return null;
            }
        }
        #endregion

        #region Color-inverting tile source
        /// <summary>
        /// Wraps an ITileSource and inverts the RGB channels of every tile,
        /// producing a dark-mode version of the original map.
        /// </summary>
        private sealed class InvertingTileSource : ITileSource
        {
            private readonly ITileSource _inner;

            public InvertingTileSource(ITileSource inner) => _inner = inner;

            public ITileSchema Schema => _inner.Schema;
            public string Name => _inner.Name + " (dark)";
            public Attribution Attribution => _inner.Attribution;

            public async Task<byte[]?> GetTileAsync(TileInfo tileInfo)
            {
                var data = await _inner.GetTileAsync(tileInfo);
                if (data == null || data.Length == 0) return data;

                return InvertPixels(data);
            }

            private static byte[] InvertPixels(byte[] tileData)
            {
                using var bitmap = SKBitmap.Decode(tileData);
                if (bitmap == null) return tileData;

                var pixels = bitmap.Pixels;
                for (int i = 0; i < pixels.Length; i++)
                {
                    var c = pixels[i];
                    pixels[i] = new SKColor(
                        (byte)(255 - c.Red),
                        (byte)(255 - c.Green),
                        (byte)(255 - c.Blue),
                        c.Alpha);
                }
                bitmap.Pixels = pixels;

                using var image = SKImage.FromBitmap(bitmap);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 90);
                return encoded.ToArray();
            }
        }
        #endregion
    }
}
