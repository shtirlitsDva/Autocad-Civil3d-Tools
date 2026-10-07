using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using IntersectUtilities.UtilsCommon.DataManager.CsvData;

namespace IntersectUtilities.MPE.AutoAmkB;

/// <summary>One kind of finding. Kinds are data: the key ties a kind to its texts in AutoAmkB.csv.</summary>
internal sealed record HitKind(string Key, string Name)
{
    public static readonly HitKind ElCrossing = new("EL_KRYDS", "Kabel ≥ 10 kV, krydsning");
    public static readonly HitKind ElAlongside = new("EL_LANGS", "Kabel ≥ 10 kV, langs med");
    public static readonly HitKind GasCrossing = new("GAS_KRYDS", "Gasledning, krydsning");
    public static readonly HitKind GasAlongside = new("GAS_LANGS", "Gasledning, langs med");
    public static readonly HitKind WaterCrossing = new("VAND_KRYDS", "Vandledning, krydsning");
    public static readonly HitKind WaterAlongside = new("VAND_LANGS", "Vandledning, langs med");
    public static readonly HitKind Depth = new("DYBDE", "Dyb udgravning");
    public static readonly HitKind NarrowRoad = new("SMALVEJ", "Smal vej");
    public static readonly HitKind Soil = new("JORD", "Jordforurening V1/V2");

    public static readonly IReadOnlyList<HitKind> All = new[]
    {
        ElCrossing, ElAlongside, GasCrossing, GasAlongside, WaterCrossing, WaterAlongside, Depth, NarrowRoad, Soil
    };
}

/// <summary>How a LER line's size is measured and compared with its threshold.</summary>
internal abstract record LerMeasure
{
    private LerMeasure() { }

    /// <summary>The placeholder the value fills in the texts, without braces.</summary>
    public abstract string Placeholder { get; }

    public abstract Option<double> Read(IReadOnlyDictionary<string, string> properties);

    public abstract string Format(double value);

    public abstract string Detail(double value);

    internal sealed record Voltage : LerMeasure
    {
        public override string Placeholder => "kV";

        // LER 2.0 writes the voltage as text, e.g. "10kV" or "0.4kV". Blank means unknown.
        public override Option<double> Read(IReadOnlyDictionary<string, string> properties) =>
            properties.Lookup("SpændingsNiveau").Bind(AmkNumbers.LeadingNumber).Where(kv => kv > 0);

        public override string Format(double value) => AmkNumbers.Text(value, "0.##");

        public override string Detail(double value) => $"{Format(value)} kV";
    }

    internal sealed record Diameter : LerMeasure
    {
        public override string Placeholder => "Ø";

        // Outer diameter as registered in LER, converted to mm. Zero means unknown.
        public override Option<double> Read(IReadOnlyDictionary<string, string> properties) =>
            properties.Lookup("UdvendigDiameter")
                .Bind(AmkNumbers.LeadingNumber)
                .Map(value => value * UnitFactor(properties.Lookup("UdvendigDiameterUnits").OrElse("mm")))
                .Where(mm => mm > 0);

        public override string Format(double value) => AmkNumbers.Text(value, "0.#");

        public override string Detail(double value) => $"Ø{Format(value)} mm";

        // LER registers diameters in mm; any other unit seen in the data is converted, an unknown one is read as mm.
        private static readonly IReadOnlyDictionary<string, double> UnitFactors =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["mm"] = 1.0, ["cm"] = 10.0, ["m"] = 1000.0 };

        private static double UnitFactor(string unit) => UnitFactors.Lookup(unit.Trim()).OrElse(1.0);
    }
}

/// <summary>A LER utility class the logbook asks about, with its threshold and hit kinds.</summary>
internal sealed record LerCriterion(
    string Name,
    string PropertySetName,
    LerMeasure Measure,
    double MinValue,
    HitKind Crossing,
    HitKind Alongside,
    bool AlongsideEnabled);

internal sealed record HitTexts(
    string Topic,
    string Description,
    string MainCategory,
    string SubCategory,
    string CriterionTopic,
    string CriterionDescription);

internal sealed record JournalTexts(string MainCategory, IReadOnlyList<string> SubCategories, string Description);

internal sealed record RuleEntry(string Key, string Value, string Note);

internal sealed record AmkRules(
    double SampleStep,
    double PointMergeDistance,
    double StretchJoinGap,
    IReadOnlyList<LerCriterion> LerCriteria,
    double AlongsideDistance,
    double AlongsideMinLength,
    double DepthLimit,
    double Bedding,
    double RoadMinFreeWidth,
    double RoadBarrierAllowance,
    double RoadSearchDistance,
    double RoadMinStretchLength,
    string RoadEdgeLayer,
    double SoilMargin,
    string SoilLayerV1,
    string SoilLayerV2,
    IReadOnlySet<string> ValveTypes,
    double ValveMergeDistance,
    IReadOnlyDictionary<string, HitTexts> Texts,
    JournalTexts Journal,
    IReadOnlyList<RuleEntry> Entries)
{
    public HitTexts TextsFor(HitKind kind) =>
        Texts.Lookup(kind.Key).OrElse(new HitTexts(kind.Name, "", "", "", kind.Name, "{antal} fund: {stationer}"));
}

internal sealed record LoadedRules(AmkRules Rules, string Source);

internal static class AmkRulesLoader
{
    public const string FileName = "AutoAmkB.csv";

    /// <summary>
    /// Reads AutoAmkB.csv from the shared Conf folder. When the file is not there yet, the built-in
    /// defaults are used and the run info says so.
    /// </summary>
    public static Result<LoadedRules> Load()
    {
        string confPath = Path.Combine(CsvRegistry.ConfPath, FileName);
        bool exists = Boundary.TryOption(() => File.Exists(confPath)).OrElse(false);

        if (exists)
        {
            return Boundary.Try(() => File.ReadAllText(confPath, Encoding.UTF8), $"Kunne ikke læse {confPath}")
                .Bind(Parse)
                .Map(rules => new LoadedRules(rules, confPath));
        }

        return Parse(AmkDefaultRules.Csv)
            .Map(rules => new LoadedRules(rules, $"Indbygget standard ({FileName} findes ikke i {CsvRegistry.ConfPath})"));
    }

    public static Result<AmkRules> Parse(string csvText)
    {
        List<RuleEntry> entries = ReadEntries(csvText);
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (RuleEntry entry in entries) values[entry.Key] = entry.Value;

        RuleReader reader = new RuleReader(values);

        double sampleStep = reader.Number("Prøveafstand");
        double pointMerge = reader.Number("Punktsammenlægning");
        double stretchJoin = reader.Number("Strækningsmellemrum");

        LerCriterion el = new LerCriterion(
            "Kabel ≥ {0} kV", "Elledning", new LerMeasure.Voltage(), reader.Number("El.MinSpænding"),
            HitKind.ElCrossing, HitKind.ElAlongside, reader.Flag("El.LangsMed"));
        LerCriterion gas = new LerCriterion(
            "Gasledning ≥ Ø{0}", "Gasledning", new LerMeasure.Diameter(), reader.Number("Gas.MinDiameter"),
            HitKind.GasCrossing, HitKind.GasAlongside, reader.Flag("Gas.LangsMed"));
        LerCriterion water = new LerCriterion(
            "Vandledning ≥ Ø{0}", "Vandledning", new LerMeasure.Diameter(), reader.Number("Vand.MinDiameter"),
            HitKind.WaterCrossing, HitKind.WaterAlongside, reader.Flag("Vand.LangsMed"));

        Dictionary<string, HitTexts> texts = HitKind.All.ToDictionary(
            kind => kind.Key,
            kind => new HitTexts(
                reader.Text($"{kind.Key}.Emne"),
                reader.Text($"{kind.Key}.Beskrivelse"),
                reader.Text($"{kind.Key}.Hovedkategori"),
                reader.Text($"{kind.Key}.Underkategori"),
                reader.Text($"{kind.Key}.KriterieEmne"),
                reader.Text($"{kind.Key}.KriterieBeskrivelse")),
            StringComparer.OrdinalIgnoreCase);

        JournalTexts journal = new JournalTexts(
            reader.Text("VENTIL.Hovedkategori"),
            new[]
            {
                reader.Text("VENTIL.Underkategori1"),
                reader.Text("VENTIL.Underkategori2"),
                reader.Text("VENTIL.Underkategori3"),
            }.Where(text => text.Length > 0).ToList(),
            reader.Text("VENTIL.Beskrivelse"));

        AmkRules rules = new AmkRules(
            sampleStep,
            pointMerge,
            stretchJoin,
            new[] { el, gas, water },
            reader.Number("LangsMed.Afstand"),
            reader.Number("LangsMed.MinLængde"),
            reader.Number("Dybde.Grænse"),
            reader.Number("Dybde.Underlag"),
            reader.Number("SmalVej.MinFriBredde"),
            reader.Number("SmalVej.Afspærring"),
            reader.Number("SmalVej.Søgeafstand"),
            reader.Number("SmalVej.MinLængde"),
            reader.Text("SmalVej.Lag"),
            reader.Number("Jord.Margin"),
            reader.Text("Jord.LagV1"),
            reader.Text("Jord.LagV2"),
            reader.Text("Ventil.Typer")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            reader.Number("Ventil.Sammenlægning"),
            texts,
            journal,
            entries);

        List<string> problems = reader.Problems.Concat(Validate(rules)).ToList();
        return problems.Count == 0
            ? Result<AmkRules>.Success(rules)
            : Result<AmkRules>.Failure($"{FileName} er ikke gyldig:{Environment.NewLine}  " + string.Join(Environment.NewLine + "  ", problems));
    }

    private static IEnumerable<string> Validate(AmkRules rules)
    {
        if (rules.SampleStep <= 0) yield return "Prøveafstand skal være større end 0.";
        if (rules.StretchJoinGap <= rules.SampleStep)
            yield return $"Strækningsmellemrum skal være større end prøveafstanden ({AmkNumbers.Meters(rules.SampleStep)}).";
        if (rules.PointMergeDistance < 0) yield return "Punktsammenlægning kan ikke være negativ (0 = ingen sammenlægning).";
        if (rules.RoadSearchDistance <= 0) yield return "SmalVej.Søgeafstand skal være større end 0.";
        if (rules.RoadMinStretchLength < 0) yield return "SmalVej.MinLængde kan ikke være negativ (0 = alle strækninger medtages).";
        if (rules.RoadEdgeLayer.Length == 0) yield return "SmalVej.Lag mangler.";
        if (rules.ValveTypes.Count == 0) yield return "Ventil.Typer mangler.";
        if (rules.Journal.SubCategories.Count == 0) yield return "VENTIL.Underkategori1-3 mangler.";
    }

    private static List<RuleEntry> ReadEntries(string csvText)
    {
        List<RuleEntry> entries = new List<RuleEntry>();
        foreach (string rawLine in csvText.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            string[] parts = line.Split(';');
            string key = parts[0].Trim().TrimStart('﻿');
            if (key.Length == 0 || key.Equals("Nøgle", StringComparison.OrdinalIgnoreCase)) continue;

            entries.Add(new RuleEntry(
                key,
                parts.Length > 1 ? parts[1].Trim() : "",
                parts.Length > 2 ? parts[2].Trim() : ""));
        }
        return entries;
    }

    /// <summary>Reads typed values and collects every problem, so one run reports them all at once.</summary>
    private sealed class RuleReader
    {
        private readonly IReadOnlyDictionary<string, string> _values;
        public List<string> Problems { get; } = new List<string>();

        public RuleReader(IReadOnlyDictionary<string, string> values) => _values = values;

        public string Text(string key) =>
            _values.Lookup(key).Match(
                value => value,
                () =>
                {
                    Problems.Add($"Nøglen '{key}' mangler.");
                    return "";
                });

        public double Number(string key) =>
            _values.Lookup(key).Match(
                value => AmkNumbers.Parse(value).Match(
                    number => number,
                    () =>
                    {
                        Problems.Add($"'{key}' skal være et tal, men er '{value}'.");
                        return 0.0;
                    }),
                () =>
                {
                    Problems.Add($"Nøglen '{key}' mangler.");
                    return 0.0;
                });

        private static readonly IReadOnlyDictionary<string, bool> FlagWords =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                ["ja"] = true, ["j"] = true, ["yes"] = true, ["true"] = true, ["1"] = true,
                ["nej"] = false, ["n"] = false, ["no"] = false, ["false"] = false, ["0"] = false,
            };

        public bool Flag(string key) =>
            _values.Lookup(key).Match(
                value => FlagWords.Lookup(value.Trim()).Match(flag => flag, () => AddFlagProblem(key, value)),
                () =>
                {
                    Problems.Add($"Nøglen '{key}' mangler.");
                    return false;
                });

        private bool AddFlagProblem(string key, string value)
        {
            Problems.Add($"'{key}' skal være Ja eller Nej, men er '{value}'.");
            return false;
        }
    }
}

/// <summary>Number parsing and Danish formatting shared by the rules, the checks and the export.</summary>
internal static class AmkNumbers
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private static readonly Regex LeadingNumberRegex = new Regex(@"-?\d+(?:[.,]\d+)?", RegexOptions.Compiled);

    /// <summary>A whole value such as "2,5" or "2.5".</summary>
    public static Option<double> Parse(string text) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? Option<double>.Of(value)
            : Option<double>.Nothing;

    /// <summary>The first number in a text such as "10kV" or "160 mm".</summary>
    public static Option<double> LeadingNumber(string text)
    {
        Match match = LeadingNumberRegex.Match(text);
        return match.Success ? Parse(match.Value) : Option<double>.Nothing;
    }

    public static string Text(double value, string format) => value.ToString(format, Danish);

    public static string Meters(double value) => $"{value.ToString("0.00", Danish)} m";
}
