using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace IntersectUtilities
{
    public class GeoJsonFeatureCollection
    {
        [JsonPropertyName("type")]
        public string Type { get; } = "FeatureCollection";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "FeatureCollection";

        [JsonPropertyName("features")]
        public List<GeoJsonFeature> Features { get; set; } = new List<GeoJsonFeature>();

        [JsonPropertyName("crs")]
        public GeoJsonCRS CRS { get; set; } = new GeoJsonCRS();

        public GeoJsonFeatureCollection(string name)
        {
            Name = name;
        }
    }

    public class GeoJsonCRS
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "name";

        [JsonPropertyName("properties")]
        public Dictionary<string, object> Properties { get; set; }

        public GeoJsonCRS()
        {
            Properties = new Dictionary<string, object>
            {
                ["name"] = "urn:ogc:def:crs:EPSG::25832"
            };
        }
    }

    public class GeoJsonFeature
    {
        [JsonPropertyName("type")]
        public string Type { get; } = "Feature";

        [JsonPropertyName("properties")]
        [JsonExtensionData]
        public Dictionary<string, object> Properties { get; set; } 
            = new Dictionary<string, object>();

        [JsonPropertyName("geometry")]
        public GeoJsonGeometry Geometry { get; set; }
    }

    [JsonDerivedType(typeof(GeoJsonPoint))]
    [JsonDerivedType(typeof(GeoJsonLineString))]
    [JsonDerivedType(typeof(GeoJsonPolygon))]
    [JsonDerivedType(typeof(GeoJsonGeometryCollection))]
    public abstract class GeoJsonGeometry
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    public class GeoJsonPoint : GeoJsonGeometry
    {
        public GeoJsonPoint()
        {
            Type = "Point";
        }

        [JsonPropertyName("coordinates")]
        public double[] Coordinates { get; set; }
    }

    public class GeoJsonLineString : GeoJsonGeometry
    {
        public GeoJsonLineString()
        {
            Type = "LineString";
        }

        [JsonPropertyName("coordinates")]
        public double[][] Coordinates { get; set; }
    }

    public class GeoJsonPolygon : GeoJsonGeometry
    {
        public GeoJsonPolygon()
        {
            Type = "Polygon";
        }

        [JsonPropertyName("coordinates")]
        public double[][][] Coordinates { get; set; }
    }

    public class GeoJsonGeometryCollection : GeoJsonGeometry
    {
        public GeoJsonGeometryCollection()
        {
            Type = "GeometryCollection";
        }

        [JsonPropertyName("geometries")]
        public List<GeoJsonGeometry> Geometries { get; set; } = new List<GeoJsonGeometry>();
    }
}
