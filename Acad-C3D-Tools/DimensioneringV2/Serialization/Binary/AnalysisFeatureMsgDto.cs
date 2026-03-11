using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Services;
using DimensioneringV2.UI.MapProperty;

using MessagePack;

using NetTopologySuite.Geometries;

using NorsynHydraulicCalc.Pipes;

using System;
using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Serialization.Binary;

[MessagePackObject]
public class AnalysisFeatureMsgDto
{
    // Geometry
    [Key(0)] public double[][] Coordinates { get; set; }
    [Key(1)] public double[][] Geometry25832 { get; set; }

    // Grunddata (source attributes, stored in attribute table)
    [Key(2)] public bool IsRootNode { get; set; }
    [Key(3)] public int NumberOfBuildingsConnected { get; set; }
    [Key(4)] public int NumberOfUnitsConnected { get; set; }
    [Key(5)] public double HeatingDemandConnected { get; set; }
    [Key(6)] public string id_lokalId { get; set; } = "";
    [Key(7)] public string Name { get; set; } = "";
    [Key(8)] public int Opf_relsesaar { get; set; }
    [Key(9)] public int BeregningsAreal { get; set; }
    [Key(10)] public double KaelderAreal { get; set; }
    [Key(11)] public string VarmeType { get; set; } = "";
    [Key(12)] public string VarmeInstallation { get; set; } = "";
    [Key(13)] public string OpvarmningsMiddel { get; set; } = "";
    [Key(14)] public string InstallationOgBraendsel { get; set; } = "";
    [Key(15)] public string Vejnavn { get; set; } = "";
    [Key(16)] public string Vejklasse { get; set; } = "";
    [Key(17)] public string Husnummer { get; set; } = "";
    [Key(18)] public string Postnr { get; set; } = "";
    [Key(19)] public string By { get; set; } = "";
    [Key(20)] public double SpecifikVarmeForbrug { get; set; }
    [Key(21)] public string VarmeDistrikt { get; set; } = "";
    [Key(22)] public int AdresseDuplikatNr { get; set; }
    [Key(23)] public string Adresse { get; set; } = "";
    [Key(24)] public string BygningsAnvendelseNyKode { get; set; } = "";
    [Key(25)] public string BygningsAnvendelseNyTekst { get; set; } = "";

    // Calculated results
    [Key(26)] public int NumberOfBuildingsSupplied { get; set; }
    [Key(27)] public int NumberOfUnitsSupplied { get; set; }
    [Key(28)] public double HeatingDemandSupplied { get; set; }
    [Key(29)] public Dim Dim { get; set; }
    [Key(30)] public double ReynoldsSupply { get; set; }
    [Key(31)] public double ReynoldsReturn { get; set; }
    [Key(32)] public double KarFlowHeatSupply { get; set; }
    [Key(33)] public double KarFlowBVSupply { get; set; }
    [Key(34)] public double KarFlowHeatReturn { get; set; }
    [Key(35)] public double KarFlowBVReturn { get; set; }
    [Key(36)] public double DimFlowSupply { get; set; }
    [Key(37)] public double DimFlowReturn { get; set; }
    [Key(38)] public double PressureGradientSupply { get; set; }
    [Key(39)] public double PressureGradientReturn { get; set; }
    [Key(40)] public double VelocitySupply { get; set; }
    [Key(41)] public double VelocityReturn { get; set; }
    [Key(42)] public double UtilizationRate { get; set; }
    [Key(43)] public double Effekt { get; set; }
    [Key(44)] public bool IsBridge { get; set; }
    [Key(45)] public int SubGraphId { get; set; }
    [Key(46)] public bool IsCriticalPath { get; set; }
    [Key(47)] public bool ManualDim { get; set; }
    [Key(48)] public double PressureLossAtClientSupply { get; set; }
    [Key(49)] public double PressureLossAtClientReturn { get; set; }
    [Key(50)] public double DifferentialPressureAtClient { get; set; }
    [Key(51)] public double TempDeltaVarme { get; set; }
    [Key(52)] public double TempDeltaBV { get; set; }

    internal static AnalysisFeatureMsgDto FromDomain(AnalysisFeature af)
    {
        var line = af.Geometry as LineString
            ?? throw new InvalidOperationException("Expected LineString geometry");

        var dto = new AnalysisFeatureMsgDto
        {
            // Geometry
            Coordinates = line.Coordinates
                .Select(c => new[] { c.X, c.Y })
                .ToArray(),
            Geometry25832 = af.Geometry25832.Coordinates
                .Select(c => new[] { c.X, c.Y })
                .ToArray(),

            // Source attributes (direct attribute table reads)
            IsRootNode = af["IsRootNode"] is bool b ? b : false,
            NumberOfBuildingsConnected = af["NumberOfBuildingsConnected"] as int? ?? 0,
            NumberOfUnitsConnected = af["NumberOfUnitsConnected"] as int? ?? 0,
            HeatingDemandConnected = af["HeatingDemandConnected"] as double? ?? 0,
            id_lokalId = af["id_lokalId"] as string ?? "",
            Name = af["Name"] as string ?? "",
            Opf_relsesaar = af["Opf\u00f8relses\u00e5r"] as int? ?? 0,
            BeregningsAreal = af["BeregningsAreal"] as int? ?? 0,
            KaelderAreal = af["K\u00e6lderAreal"] as double? ?? 0,
            VarmeType = af["VarmeType"] as string ?? "",
            VarmeInstallation = af["VarmeInstallation"] as string ?? "",
            OpvarmningsMiddel = af["OpvarmningsMiddel"] as string ?? "",
            InstallationOgBraendsel = af["InstallationOgBr\u00e6ndsel"] as string ?? "",
            Vejnavn = af["Vejnavn"] as string ?? "",
            Vejklasse = af["Vejklasse"] as string ?? "",
            Husnummer = af["Husnummer"] as string ?? "",
            Postnr = af["Postnr"] as string ?? "",
            By = af["By"] as string ?? "",
            SpecifikVarmeForbrug = af["SpecifikVarmeForbrug"] as double? ?? 0,
            VarmeDistrikt = af["VarmeDistrikt"] as string ?? "",
            AdresseDuplikatNr = af["AdresseDuplikatNr"] as int? ?? 0,
            Adresse = af["Adresse"] as string ?? "",
            BygningsAnvendelseNyKode = af.GetAttributeValue<string>(MapPropertyEnum.BygningsAnvendelseNyKode) ?? "",
            BygningsAnvendelseNyTekst = af.GetAttributeValue<string>(MapPropertyEnum.BygningsAnvendelseNyTekst) ?? "",

            // Calculated results (MapProperty-backed)
            NumberOfBuildingsSupplied = af.GetAttributeValue<int>(MapPropertyEnum.Bygninger),
            NumberOfUnitsSupplied = af.GetAttributeValue<int>(MapPropertyEnum.Units),
            HeatingDemandSupplied = af.GetAttributeValue<double>(MapPropertyEnum.HeatingDemand),
            Dim = af.GetAttributeValue<Dim>(MapPropertyEnum.Pipe),
            DimFlowSupply = af.GetAttributeValue<double>(MapPropertyEnum.DimFlowSupply),
            DimFlowReturn = af.GetAttributeValue<double>(MapPropertyEnum.DimFlowReturn),
            // Store RAW pressure gradient (without pct multiplier)
            PressureGradientSupply = af.GetAttributeValue<double>(MapPropertyEnum.PressureGradientSupply),
            PressureGradientReturn = af.GetAttributeValue<double>(MapPropertyEnum.PressureGradientReturn),
            VelocitySupply = af.GetAttributeValue<double>(MapPropertyEnum.VelocitySupply),
            VelocityReturn = af.GetAttributeValue<double>(MapPropertyEnum.VelocityReturn),
            UtilizationRate = af.GetAttributeValue<double>(MapPropertyEnum.UtilizationRate),
            IsBridge = af.GetAttributeValue<bool>(MapPropertyEnum.Bridge),
            SubGraphId = af.GetAttributeValue<int>(MapPropertyEnum.SubGraphId),
            IsCriticalPath = af.GetAttributeValue<bool>(MapPropertyEnum.CriticalPath),
            ManualDim = af.GetAttributeValue<bool>(MapPropertyEnum.ManualDim),
            TempDeltaVarme = af.GetAttributeValue<double>(MapPropertyEnum.TempDeltaVarme),
            TempDeltaBV = af.GetAttributeValue<double>(MapPropertyEnum.TempDeltaBV),

            // Direct attribute reads for non-MapProperty calculated fields
            ReynoldsSupply = af["ReynoldsSupply"] as double? ?? 0,
            ReynoldsReturn = af["ReynoldsReturn"] as double? ?? 0,
            KarFlowHeatSupply = af["KarFlowHeatSupply"] as double? ?? 0,
            KarFlowBVSupply = af["KarFlowBVSupply"] as double? ?? 0,
            KarFlowHeatReturn = af["KarFlowHeatReturn"] as double? ?? 0,
            KarFlowBVReturn = af["KarFlowBVReturn"] as double? ?? 0,
            Effekt = af["Effekt"] as double? ?? 0,
            PressureLossAtClientSupply = af["PressureLossAtClientSupply"] as double? ?? 0,
            PressureLossAtClientReturn = af["PressureLossAtClientReturn"] as double? ?? 0,
            DifferentialPressureAtClient = af["DifferentialPressureAtClient"] as double? ?? 0,
        };

        return dto;
    }

    internal AnalysisFeature ToDomain()
    {
        // Build geometry from coordinates
        var geometry25832 = new LineString(
            Geometry25832.Select(c => new Coordinate(c[0], c[1])).ToArray());

        var displayGeometry = new LineString(
            Coordinates.Select(c => new Coordinate(c[0], c[1])).ToArray());

        // Build attributes dictionary — same pattern as the JSON DTO
        var attributes = new Dictionary<string, object>();

        // Source attributes
        AddIfNotDefault(attributes, "IsRootNode", IsRootNode);
        AddIfNotDefault(attributes, "NumberOfBuildingsConnected", NumberOfBuildingsConnected);
        AddIfNotDefault(attributes, "NumberOfUnitsConnected", NumberOfUnitsConnected);
        AddIfNotDefault(attributes, "HeatingDemandConnected", HeatingDemandConnected);
        AddIfNotDefault(attributes, "id_lokalId", id_lokalId);
        AddIfNotDefault(attributes, "Name", Name);
        AddIfNotDefault(attributes, "Opf\u00f8relses\u00e5r", Opf_relsesaar);
        AddIfNotDefault(attributes, "BeregningsAreal", BeregningsAreal);
        AddIfNotDefault(attributes, "K\u00e6lderAreal", KaelderAreal);
        AddIfNotDefault(attributes, "VarmeType", VarmeType);
        AddIfNotDefault(attributes, "VarmeInstallation", VarmeInstallation);
        AddIfNotDefault(attributes, "OpvarmningsMiddel", OpvarmningsMiddel);
        AddIfNotDefault(attributes, "InstallationOgBr\u00e6ndsel", InstallationOgBraendsel);
        AddIfNotDefault(attributes, "Vejnavn", Vejnavn);
        AddIfNotDefault(attributes, "Vejklasse", Vejklasse);
        AddIfNotDefault(attributes, "Husnummer", Husnummer);
        AddIfNotDefault(attributes, "Postnr", Postnr);
        AddIfNotDefault(attributes, "By", By);
        AddIfNotDefault(attributes, "SpecifikVarmeForbrug", SpecifikVarmeForbrug);
        AddIfNotDefault(attributes, "VarmeDistrikt", VarmeDistrikt);
        AddIfNotDefault(attributes, "AdresseDuplikatNr", AdresseDuplikatNr);
        AddIfNotDefault(attributes, "Adresse", Adresse);

        // MapProperty-backed attributes — use the property name as key
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.BygningsAnvendelseNyKode), BygningsAnvendelseNyKode);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.BygningsAnvendelseNyTekst), BygningsAnvendelseNyTekst);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.Bygninger), NumberOfBuildingsSupplied);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.Units), NumberOfUnitsSupplied);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.HeatingDemand), HeatingDemandSupplied);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.Pipe), Dim);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.DimFlowSupply), DimFlowSupply);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.DimFlowReturn), DimFlowReturn);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.PressureGradientSupply), PressureGradientSupply);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.PressureGradientReturn), PressureGradientReturn);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.VelocitySupply), VelocitySupply);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.VelocityReturn), VelocityReturn);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.UtilizationRate), UtilizationRate);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.Bridge), IsBridge);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.SubGraphId), SubGraphId);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.CriticalPath), IsCriticalPath);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.ManualDim), ManualDim);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.TempDeltaVarme), TempDeltaVarme);
        AddIfNotDefault(attributes, AnalysisFeature.GetAttributeName(MapPropertyEnum.TempDeltaBV), TempDeltaBV);

        // Non-MapProperty calculated fields
        AddIfNotDefault(attributes, "ReynoldsSupply", ReynoldsSupply);
        AddIfNotDefault(attributes, "ReynoldsReturn", ReynoldsReturn);
        AddIfNotDefault(attributes, "KarFlowHeatSupply", KarFlowHeatSupply);
        AddIfNotDefault(attributes, "KarFlowBVSupply", KarFlowBVSupply);
        AddIfNotDefault(attributes, "KarFlowHeatReturn", KarFlowHeatReturn);
        AddIfNotDefault(attributes, "KarFlowBVReturn", KarFlowBVReturn);
        AddIfNotDefault(attributes, "Effekt", Effekt);
        AddIfNotDefault(attributes, "PressureLossAtClientSupply", PressureLossAtClientSupply);
        AddIfNotDefault(attributes, "PressureLossAtClientReturn", PressureLossAtClientReturn);
        AddIfNotDefault(attributes, "DifferentialPressureAtClient", DifferentialPressureAtClient);

        // Construct AnalysisFeature using the 3-arg constructor (displayGeometry, geometry25832, attributes)
        return new AnalysisFeature(displayGeometry, geometry25832, attributes);
    }

    private static void AddIfNotDefault(Dictionary<string, object> dict, string key, object value)
    {
        if (IsDefault(value)) return;
        dict[key] = value;
    }

    private static bool IsDefault(object? value) => value switch
    {
        null => true,
        string s => s.Length == 0,
        int i => i == 0,
        double d => d == 0.0,
        bool b => !b,
        _ => false,
    };
}
