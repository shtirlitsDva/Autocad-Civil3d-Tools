using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;

namespace IntersectUtilities.NSTBL
{
    internal abstract class IntersectResult
    {
        public IntersectType IntersectType { get; set; }
        public string Vejnavn { get; set; }
        public string Vejklasse { get; set; }
        public string Belægning { get; set; }
        public string Navn { get; set; }
        public string DN1 { get; set; }
        public string DN2 { get; set; }
        public string System { get; set; }
        public string Serie { get; set; }
        public string SystemType { get; set; }
        /// <summary>
        /// Standard delivery length in metres (12/16 for steel, 100 for coils).
        /// Only pipes carry a value; components leave it empty.
        /// Written to the first reserved CWO field (Data!I).
        /// </summary>
        public string StdLength { get; set; }
        public virtual string ToString(ExportType exportType)
        {
            string dn2Value = DN2 == "0" ? "" : DN2;
            switch (exportType)
            {
                case ExportType.Unknown:
                    break;
                case ExportType.CWO:
                    return $"{Vejnavn};Vejkl. {Vejklasse};{Belægning};{Navn};{StdLength};;{DN1};{dn2Value};{System};{Serie};";
                case ExportType.JJR:
                    return $"Vejkl. {Vejklasse};{Belægning};{Navn};{DN1};{dn2Value};{System};{Serie};";
                default:
                    break;
            }
            return default;
        }
        /// <summary>
        /// One row of the CWO tilbudsliste sheet:
        /// Egne noter;Vejklasse;Belægningstype;Komponent;StdLength;Materiale;DN;DN;Rørsystem;Serie;Antal.
        /// Egne noter stays empty - the etape is written there in the sheet.
        /// SystemType holds the material (PipeSystemEnum) for pipes and components alike.
        /// </summary>
        public string ToCwoV2Row()
        {
            string dn2Value = DN2 == "0" ? "" : DN2;
            return $";Vejkl. {Vejklasse};{Belægning};{Navn};{StdLength};{SystemType};" +
                $"{DN1};{dn2Value};{System};{Serie};{AntalCwoV2}";
        }
        /// <summary>Metres for a pipe, pieces for a component.</summary>
        protected abstract string AntalCwoV2 { get; }
    }
    internal class IntersectResultPipe : IntersectResult
    {
        public IntersectResultPipe()
        {
            IntersectType = IntersectType.Pipe;
            Navn = "Rør præisoleret";
        }
        public double Antal { get; set; }
        public double Length { get; set; }
        public override string ToString(ExportType exportType) => 
            base.ToString(exportType) + $"{Antal.ToString(new CultureInfo("da-DK"))};" +
            $"{Length.ToString(new CultureInfo("da-DK"))};{SystemType}";
        protected override string AntalCwoV2 => Antal.ToString(new CultureInfo("da-DK"));
    }
    internal class IntersectResultComponent : IntersectResult
    {
        public IntersectResultComponent()
        {
            IntersectType = IntersectType.Component;
        }
        public int Count { get; set; }
        public override string ToString(ExportType exportType) => base.ToString(exportType) + $"{Count};;{SystemType}";
        protected override string AntalCwoV2 => Count.ToString(CultureInfo.InvariantCulture);
    }
    internal abstract class PropertyConfig
    {
        public bool Vejnavn { get; set; }
        public bool Vejklasse { get; set; }
        public bool Belægning { get; set; }
        public bool Navn { get; set; }
        public bool DN1 { get; set; }
        public bool DN2 { get; set; }
        public bool System { get; set; }
        public bool Serie { get; set; }
        public bool SystemType { get; set; }
    }
    public enum IntersectType
    {
        Unknown,
        Pipe,
        Component
    }
    public enum ExportType
    {
        Unknown,
        CWO,
        JJR
    }
}
