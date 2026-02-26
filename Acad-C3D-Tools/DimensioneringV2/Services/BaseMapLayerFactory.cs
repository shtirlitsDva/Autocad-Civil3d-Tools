using BruTile.Predefined;
using BruTile.Web;

using Mapsui;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;

namespace DimensioneringV2.Services
{
    internal static class BaseMapLayerFactory
    {
        public const string BaseMapLayerName = "BaseMap";
        public const string BaseMapLabelsLayerName = "BaseMap_Labels";

        // TODO: Move credentials to configuration / settings file
        private const string DatafordelerUsername = "XXXXXX";
        private const string DatafordelerPassword = "YYYYYY";

        private static readonly Color OsmBackColor = Color.White;
        private static readonly Color DarkBackColor = Color.Black;

        public static void ApplyBaseMap(Map map, BaseMapType type)
        {
            RemoveBaseMapLayers(map);

            map.BackColor = type == BaseMapType.OpenStreetMap
                ? OsmBackColor
                : DarkBackColor;

            switch (type)
            {
                case BaseMapType.OpenStreetMap:
                    var osmLayer = Mapsui.Tiling.OpenStreetMap.CreateTileLayer();
                    osmLayer.Name = BaseMapLayerName;
                    map.Layers.Insert(0, osmLayer);
                    break;

                case BaseMapType.Dark:
                    map.Layers.Insert(0, CreateCartoDarkLayer());
                    break;

                case BaseMapType.Ortofoto:
                    map.Layers.Insert(0, CreateOrtofotoLayer());
                    break;

                case BaseMapType.Hybrid:
                    map.Layers.Insert(0, CreateOrtofotoLayer());
                    map.Layers.Insert(1, CreateCartoLabelsLayer());
                    break;

                case BaseMapType.Off:
                    // No tile layers — black background only
                    break;
            }
        }

        public static void RemoveBaseMapLayers(Map map)
        {
            map.Layers.Remove(
                l => l.Name == BaseMapLayerName || l.Name == BaseMapLabelsLayerName);
        }

        private static TileLayer CreateCartoDarkLayer()
        {
            var source = new HttpTileSource(
                new GlobalSphericalMercator(),
                "https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}.png",
                new[] { "a", "b", "c", "d" },
                name: "CartoDB Dark Matter",
                attribution: new BruTile.Attribution(
                    "© OpenStreetMap contributors © CARTO",
                    "https://carto.com/attributions"));

            return new TileLayer(source) { Name = BaseMapLayerName };
        }

        private static TileLayer CreateOrtofotoLayer()
        {
            var url =
                "https://services.datafordeler.dk/GeoDanmarkOrto/orto_foraar_webm/1.0.0/WMTS"
                + $"?username={DatafordelerUsername}&password={DatafordelerPassword}"
                + "&SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0"
                + "&STYLE=default&FORMAT=image/jpeg"
                + "&TILEMATRIXSET=DFD_GoogleMapsCompatible"
                + "&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}"
                + "&Layer=orto_foraar_webm";

            var source = new HttpTileSource(
                new GlobalSphericalMercator(),
                url,
                name: "Datafordeler Ortofoto",
                attribution: new BruTile.Attribution(
                    "© SDFI / Datafordeler",
                    "https://datafordeler.dk"));

            return new TileLayer(source) { Name = BaseMapLayerName };
        }

        private static TileLayer CreateCartoLabelsLayer()
        {
            var source = new HttpTileSource(
                new GlobalSphericalMercator(),
                "https://{s}.basemaps.cartocdn.com/dark_only_labels/{z}/{x}/{y}.png",
                new[] { "a", "b", "c", "d" },
                name: "CartoDB Labels",
                attribution: new BruTile.Attribution(
                    "© OpenStreetMap contributors © CARTO",
                    "https://carto.com/attributions"));

            return new TileLayer(source) { Name = BaseMapLabelsLayerName };
        }
    }
}
