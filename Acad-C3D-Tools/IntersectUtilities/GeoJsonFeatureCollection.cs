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

        public GeoJsonFeatureCollection() { }

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
        public Dictionary<string, object> Properties { get; set; } 
            = new Dictionary<string, object>();

        [JsonPropertyName("geometry")]
        public GeoJsonGeometryBase Geometry { get; set; }

        public GeoJsonFeature() { }
    }

    [JsonDerivedType(typeof(GeoJsonGeometryPoint),
        typeDiscriminator:"point")]
    [JsonDerivedType(typeof(GeoJsonGeometryLineString),
        typeDiscriminator:"lineString")]
    [JsonDerivedType(typeof(GeoJsonGeometryPolygon), 
        typeDiscriminator:"polygon")]
    [JsonDerivedType(typeof(GeoJsonGeometryCollection),
        typeDiscriminator:"collection")]
    public abstract class GeoJsonGeometryBase
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    public class GeoJsonGeometryPoint : GeoJsonGeometryBase
    {
        public GeoJsonGeometryPoint()
        {
            Type = "Point";
        }

        [JsonPropertyName("coordinates")]
        public double[] Coordinates { get; set; }
    }

    public class GeoJsonGeometryLineString : GeoJsonGeometryBase
    {
        public GeoJsonGeometryLineString()
        {
            Type = "LineString";
        }

        [JsonPropertyName("coordinates")]
        public double[][] Coordinates { get; set; }
    }

    public class GeoJsonGeometryPolygon : GeoJsonGeometryBase
    {
        public GeoJsonGeometryPolygon()
        {
            Type = "Polygon";
        }

        [JsonPropertyName("coordinates")]
        public double[][][] Coordinates { get; set; }
    }

    public class GeoJsonGeometryCollection : GeoJsonGeometryBase
    {
        public GeoJsonGeometryCollection()
        {
            Type = "GeometryCollection";
        }

        [JsonPropertyName("geometries")]
        public List<GeoJsonGeometryBase> Geometries { get; set; } = new List<GeoJsonGeometryBase>();
    }
}
