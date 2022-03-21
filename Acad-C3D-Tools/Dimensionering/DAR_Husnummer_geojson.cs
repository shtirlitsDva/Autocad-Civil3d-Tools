using System;
using System.Data;
using System.Globalization;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IntersectUtilities.Dimensionering
{
    public class FeatureCollection
    {
        public string type { get; set; } = "FeatureCollection";
        public string name { get; set; } = "Husnumre";
        public Crs crs { get; set; } = new Crs();
        public List<Feature> features { get; set; }
        public FeatureCollection(int count) { features = new List<Feature>(count); }
        public FeatureCollection() { features = new List<Feature>(); }
    }

    public class Crs
    {
        public string type { get; set; } = "name";
        public PropertiesCrs properties { get; set; } = new PropertiesCrs("urn:ogc:def:crs:EPSG::25832");
    }
    public class PropertiesCrs
    {
        public string name { get; set; }
        public PropertiesCrs(string name) { this.name = name; }
    }
    public class Feature
    {
        public string type { get; set; } = "Feature";
        public Properties properties { get; set; } = new Properties();
        public Geometry geometry { get; set; }
        public Feature() { }
        public Feature(PointStringConvert data) { geometry = new Geometry(data); }
    }

    public class Properties
    {
        public string adgangsadressebetegnelse { get; set; }
        public string adgangTilBygning { get; set; }
        public string geoDanmarkBygning { get; set; }
        public string husnummerretning { get; set; }
        public string husnummertekst { get; set; }
        public string id_lokalId { get; set; }
        public string status { get; set; }
        public string vejmidte { get; set; }
        public DateTime virkningFra { get; set; }
        public string placeretPåForeløbigtJordstykke { get; set; }
        public string adgangTilTekniskAnlæg { get; set; }
        public string Adresse 
        { 
            get
            {
                var split = adgangsadressebetegnelse.Split(',');
                return split[0];
            }
        }
    }
    public class Geometry
    {
        public string type { get; set; } = "Point";
        public double[] coordinates;
        public Geometry() { }
        public Geometry(PointStringConvert data)
        {
            coordinates = new double[2] { data.x, data.y };
        }
    }
    public class PointStringConvert
    {
        public double x;
        public double y;
        public PointStringConvert(string data)
        {
            data = data.Replace("POINT(", "");
            data = data.Replace(")", "");
            var coords = data.Split(null);
            x = double.Parse(coords[0], System.Globalization.CultureInfo.InvariantCulture);
            y = double.Parse(coords[1], System.Globalization.CultureInfo.InvariantCulture);
        }
        public override string ToString()
        {
            return "x = " + x.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ", " + "y = " + y.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
