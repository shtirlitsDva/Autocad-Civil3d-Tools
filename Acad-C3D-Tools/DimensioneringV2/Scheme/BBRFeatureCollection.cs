using System;
using System.Data;
using System.Globalization;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;

namespace DimensioneringV2.Schema
{
    #region NewFormat

    public class BBRFeatureCollection
    {
        public string type { get; set; } = "FeatureCollection";
        public string name { get; set; } = "BBR data";
        public Crs crs { get; set; } = new Crs();
        public List<Feature> features { get; set; }

        public BBRFeatureCollection(int count) { features = new List<Feature>(count); }
        public BBRFeatureCollection() { features = new List<Feature>(); }
    }

    public class Crs
    {
        public string type { get; set; } = "name";
        public PropertiesCrs properties { get; set; } = new PropertiesCrs("urn:ogc:def:crs:EPSG::25832");
    }
    public class PropertiesCrs
    {
        public string name { get; set; }
        public PropertiesCrs() { }
        public PropertiesCrs(string name) { this.name = name; }
    }

    public class Feature
    {
        public string type { get; set; } = "Feature";
        public Properties properties { get; set; } = new Properties();
        public Geometry geometry { get; set; }
        public Feature() { }
        public Feature(PointStringConvert data) { geometry = new Geometry(data); }

        public void PopulateExcelData(DataTable koder, DataTable varmeForbrug)
        {
            properties.BygningsAnvendelseGlKode =
                ReadStringParameterFromDataTable(properties.BygningsAnvendelseNyTekst.ToString(), koder, "Translation", 1);

            string result = ReadStringParameterFromDataTable(properties.BygningsAnvendelseNyTekst.ToString(), koder, "Beholdes", 1);
            if (result == "1") properties.Beholdes = true;

            string YearRange = YearRangeSelector.SelectYearRange(properties.Opførelsesår);
            string rawVarmeForbrug = ReadStringParameterFromDataTable(
                properties.BygningsAnvendelseGlKode, varmeForbrug, YearRange, 0);

            properties.BygningsAnvendelseGlTekst = ReadStringParameterFromDataTable(
                properties.BygningsAnvendelseGlKode, varmeForbrug, "Beskrivelse", 0);

            double specifikVarmeForbrug = TryParse(rawVarmeForbrug);
            properties.SpecifikVarmeForbrug = specifikVarmeForbrug;

            double TryParse(string s)
            {
                if (string.IsNullOrEmpty(s)) return 0;
                double dResult;
                if (double.TryParse(s, NumberStyles.AllowDecimalPoint,
                                    CultureInfo.InvariantCulture, out dResult))
                    return dResult;
                else return 0;
            }

            double beregnetVarmeForbrug = Math.Round(specifikVarmeForbrug *
                (properties.SamletBoligareal + properties.SamletErhvervsareal) / 1000, 6, MidpointRounding.AwayFromZero);
            properties.EstimeretVarmeForbrug = beregnetVarmeForbrug;
        }
    }

    public class Properties
    {
        public string id_lokalId { get; set; }
        public string id_husnummerid { get; set; }
        public string Name { get; set; }
        public int Bygningsnummer { get; set; }
        public string BygningsAnvendelseNyTekst { get; set; }
        public string BygningsAnvendelseNyKode { get; set; }
        public string BygningsAnvendelseGlTekst { get; set; }
        public string BygningsAnvendelseGlKode { get; set; }
        public int Opførelsesår { get; set; }
        public int SamletBygningsareal { get; set; }
        public int SamletBoligareal { get; set; }
        public int SamletErhvervsareal { get; set; }
        public int BebyggetAreal { get; set; }
        [JsonPropertyName("KaelderAreal")]
        public int KælderAreal { get; set; }
        public string VarmeInstallation { get; set; }
        public string OpvarmningsMiddel { get; set; }
        public string Status { get; set; }
        public string Vejnavn { get; set; }
        public string Husnummer { get; set; }
        public string Postnr { get; set; }
        public string By { get; set; }
        public string ReasonForFail { get; set; }
        public bool Beholdes { get; set; } = false;
        [JsonPropertyName("Installation og braendsel")]
        public string InstallationOgBraendsel { get; set; }
        [JsonPropertyName("Distriktets navn")]
        public string DistriktetsNavn { get; set; }
        public string Bemaerkninger { get; set; }
        public double SpecifikVarmeForbrug { get; set; } = 0;
        public double EstimeretVarmeForbrug { get; set; } = 0;
        public string Adresse { get; set; }
        public string InstallationOgBrændsel { get
            {
                if (!string.IsNullOrEmpty(VarmeInstallation) && !string.IsNullOrEmpty(OpvarmningsMiddel))
                    return $"{VarmeInstallation} - {OpvarmningsMiddel}";
                else if (string.IsNullOrEmpty(VarmeInstallation) && string.IsNullOrEmpty(OpvarmningsMiddel)) return "";
                else if (!string.IsNullOrEmpty(VarmeInstallation)) return VarmeInstallation;
                else if (!string.IsNullOrEmpty(OpvarmningsMiddel)) return OpvarmningsMiddel;
                else throw new Exception("Et eller andet er super galt!" +
                                        $"{VarmeInstallation} - {OpvarmningsMiddel}");
            } }
        public string Type
        {
            get => Csv.InstOgBr.Type(InstallationOgBrændsel) ?? "";
        }
    }

    [JsonConverter(typeof(GeometryConverter))]
    public class Geometry
    {
        public string type { get; set; } = "Point";
        public double[] coordinates { get; set; }
        public Geometry() { }
        public Geometry(PointStringConvert data)
        {
            coordinates = new double[2] { data.x, data.y };
        }
    }

    public class GeometryConverter : JsonConverter<Geometry>
    {
        public override Geometry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            var geometry = new Geometry();

            if (root.TryGetProperty("type", out var typeProp))
                geometry.type = typeProp.GetString() ?? "Point";

            if (root.TryGetProperty("coordinates", out var coordsProp)
                && coordsProp.ValueKind == JsonValueKind.Array
                && coordsProp.GetArrayLength() > 0)
            {
                var first = coordsProp[0];
                if (first.ValueKind == JsonValueKind.Number)
                {
                    geometry.coordinates = coordsProp.Deserialize<double[]>();
                }
                else if (first.ValueKind == JsonValueKind.Array)
                {
                    var nested = coordsProp.Deserialize<double[][]>();
                    if (nested?.Length > 0)
                        geometry.coordinates = nested[0];
                }
            }

            return geometry;
        }

        public override void Write(Utf8JsonWriter writer, Geometry value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("type", value.type);
            writer.WritePropertyName("coordinates");
            JsonSerializer.Serialize(writer, value.coordinates, options);
            writer.WriteEndObject();
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

    internal static class YearRangeSelector
    {
        internal static string SelectYearRange(int OPFOERELSE_AA)
        {
            switch (OPFOERELSE_AA)
            {
                case int n when (n >= 1200 && n <= 1850):
                    return "1200-1850";
                case int n when (n >= 1851 && n <= 1930):
                    return "1851-1930";
                case int n when (n >= 1931 && n <= 1950):
                    return "1931-1950";
                case int n when (n >= 1951 && n <= 1960):
                    return "1951-1960";
                case int n when (n >= 1961 && n <= 1972):
                    return "1961-1972";
                case int n when (n >= 1973 && n <= 1978):
                    return "1973-1978";
                case int n when (n >= 1979 && n <= 1998):
                    return "1979-1998";
                case int n when (n >= 1999 && n <= 2007):
                    return "1999-2007";
                case int n when (n >= 2008 && n <= 2015):
                    return "2008-2015";
                case int n when (n >= 2016 && n <= 2020):
                    return "2016-2020";
                default:
                    return "null";
            }
        }
    }

    #endregion
}
