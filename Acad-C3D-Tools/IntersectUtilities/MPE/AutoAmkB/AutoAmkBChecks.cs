using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.LongitudinalProfiles;
using IntersectUtilities.NTS;
using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NetTopologySuite.Geometries;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.UtilsCommon.Utils;

using AcEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using AecPropertyDataServices = Autodesk.Aec.PropertyData.DatabaseServices.PropertyDataServices;
using AecPropertyDefinition = Autodesk.Aec.PropertyData.DatabaseServices.PropertyDefinition;
using AecPropertySet = Autodesk.Aec.PropertyData.DatabaseServices.PropertySet;
using AecPropertySetDefinition = Autodesk.Aec.PropertyData.DatabaseServices.PropertySetDefinition;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using NtsLineString = NetTopologySuite.Geometries.LineString;

namespace IntersectUtilities.MPE.AutoAmkB;

internal static class PropertySetValues
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> NoSets =
        new Dictionary<string, IReadOnlyDictionary<string, string>>();

    /// <summary>
    /// Every property of every property set on the entity, as text, keyed by set name. Opens nothing for
    /// write, so unlike PropertySetManager it never attaches a set to an entity that has none.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Read(AcEntity entity) =>
        Boundary.TryOption(() => ReadSets(entity)).OrElse(NoSets);

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ReadSets(AcEntity entity)
    {
        Dictionary<string, IReadOnlyDictionary<string, string>> sets =
            new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        using Transaction tx = entity.Database.TransactionManager.StartOpenCloseTransaction();
        foreach (ObjectId setId in AecPropertyDataServices.GetPropertySets(entity))
        {
            AecPropertySet set = (AecPropertySet)tx.GetObject(setId, OpenMode.ForRead);
            AecPropertySetDefinition definition =
                (AecPropertySetDefinition)tx.GetObject(set.PropertySetDefinition, OpenMode.ForRead);

            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (AecPropertyDefinition property in definition.Definitions)
            {
                object value = set.GetAt(set.PropertyNameToId(property.Name));
                values[property.Name] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            }
            sets[set.PropertySetDefinitionName] = values;
        }
        return sets;
    }

    /// <summary>The alignment a Fremtid object is assigned to (DriPipelineData.BelongsToAlignment).</summary>
    public static Option<string> AlignmentOf(AcEntity entity) =>
        Read(entity).Lookup("DriPipelineData")
            .Bind(values => values.Lookup("BelongsToAlignment"))
            .Map(name => name.Trim())
            .Where(name => name.Length > 0 && !name.Equals("NA", StringComparison.OrdinalIgnoreCase));
}

/// <summary>A LER line that meets one of the logbook criteria.</summary>
internal sealed record LerLine(
    LerCriterion Criterion,
    double Value,
    string Type,
    string Owner,
    string Id,
    string SourceFile,
    string Handle);

internal static class LerClassifier
{
    /// <summary>
    /// The criterion the line meets: its property set matches, it is in service, and its voltage or
    /// diameter is known and at least the threshold. Lines out of service or with unknown values are
    /// left out, as decided for AUTOAMKB.
    /// </summary>
    public static Option<LerLine> Classify(AcEntity entity, IReadOnlyList<LerCriterion> criteria, string sourceFile)
    {
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> sets = PropertySetValues.Read(entity);
        return criteria
            .Select(criterion => sets.Lookup(criterion.PropertySetName)
                .Bind(values => Line(criterion, values, entity.Handle.ToString(), sourceFile)))
            .Values()
            .FirstOption();
    }

    private static Option<LerLine> Line(
        LerCriterion criterion, IReadOnlyDictionary<string, string> values, string handle, string sourceFile)
    {
        bool inService = values.Lookup("Driftsstatus")
            .Map(status => status.Trim().Equals("i drift", StringComparison.OrdinalIgnoreCase))
            .OrElse(false);
        if (!inService) return Option<LerLine>.Nothing;

        return criterion.Measure.Read(values)
            .Where(value => value >= criterion.MinValue - 1e-9)
            .Map(value => new LerLine(
                criterion,
                value,
                values.Lookup("Type").OrElse(""),
                values.Lookup("LedningsEjersNavn").OrElse(""),
                Identity(values, handle),
                sourceFile,
                handle));
    }

    // GmlId identifies a line across overlapping LER requests; LerId is only unique per owner.
    private static string Identity(IReadOnlyDictionary<string, string> values, string handle) =>
        values.Lookup("GmlId").Where(id => id.Length > 0)
            .Match(id => id, () => values.Lookup("LerId").Where(id => id.Length > 0).OrElse(handle));
}

/// <summary>Trench width along one alignment, from the pipe sizes in the Fremtid drawing.</summary>
internal sealed class TrenchWidthMap
{
    private readonly IReadOnlyList<TrenchRange> _ranges;

    private TrenchWidthMap(IReadOnlyList<TrenchRange> ranges) => _ranges = ranges;

    public static readonly TrenchWidthMap Unknown = new TrenchWidthMap(Array.Empty<TrenchRange>());

    public Option<double> At(double station) =>
        _ranges
            .Where(range => station >= range.From - 1e-6 && station <= range.To + 1e-6)
            .Select(range => range.Width)
            .FirstOption()
            .Bind(width => width);

    public static Result<TrenchWidthMap> Build(Alignment alignment, IReadOnlyList<AcEntity> pipelineEntities) =>
        Boundary.Try(
                () => PipelineSizeArrayFactory.CreateSizeArray(PipelineV2Factory.Create(pipelineEntities, alignment)),
                $"Rørdimensioner langs alignment {alignment.Name} kunne ikke bestemmes")
            .Map(sizes => new TrenchWidthMap(sizes.Sizes.Select(ToRange).ToList()));

    // The pipe schedule answers 1 000 000 mm for a size it does not know; that is an unknown width.
    private static TrenchRange ToRange(SizeEntryV2 size) =>
        new TrenchRange(
            size.StartStation,
            size.EndStation,
            size.DN <= 0
                ? Option<double>.Nothing
                : Boundary.TryOption(() => GetTrenchWidth(size.DN, size.System, size.Type, size.Series))
                    .Where(millimetres => millimetres > 0 && millimetres < 999_999)
                    .Map(millimetres => millimetres / 1000.0));

    private sealed record TrenchRange(double From, double To, Option<double> Width);
}

/// <summary>Finds the sample nearest a station, to give stretch hits a plan position.</summary>
internal sealed class SampleLookup
{
    private readonly IReadOnlyList<StationSample> _samples;

    public SampleLookup(IReadOnlyList<StationSample> samples) => _samples = samples;

    public Option<StationSample> Nearest(double station)
    {
        if (_samples.Count == 0) return Option<StationSample>.Nothing;
        int low = 0, high = _samples.Count - 1;
        while (low < high)
        {
            int middle = (low + high) / 2;
            if (_samples[middle].Station < station) low = middle + 1;
            else high = middle;
        }
        int best = low > 0 && Math.Abs(_samples[low - 1].Station - station) < Math.Abs(_samples[low].Station - station)
            ? low - 1
            : low;
        return Option<StationSample>.Of(_samples[best]);
    }
}

internal static class LerCrossingCheck
{
    private sealed record Crossing(LerLine Line, double Station, Point3d Point);

    public static IReadOnlyList<Hit> Run(Alignment alignment, ILer3dManager ler, AmkRules rules, ICollection<string> warnings) =>
        Boundary.Try(() => ler.GetIntersectingEntities(alignment), $"LER-krydsninger for alignment {alignment.Name} kunne ikke findes")
            .Match(
                entities => Hits(alignment, ler, entities, rules),
                fault =>
                {
                    warnings.Add(fault);
                    return (IReadOnlyList<Hit>)Array.Empty<Hit>();
                });

    private static IReadOnlyList<Hit> Hits(Alignment alignment, ILer3dManager ler, IEnumerable<AcEntity> entities, AmkRules rules)
    {
        List<Crossing> crossings = entities
            .SelectMany(entity => LerClassifier.Classify(entity, rules.LerCriteria, SourceName(entity))
                .Match(line => CrossingsOf(alignment, ler, entity, line), Array.Empty<Crossing>))
            .ToList();

        // Overlapping LER requests can hold the same line twice: keep one crossing per line and place.
        IEnumerable<Crossing> distinct = crossings
            .GroupBy(crossing => (crossing.Line.Criterion.Crossing.Key, crossing.Line.Id, Math.Round(crossing.Station, 1)))
            .Select(group => group.First());

        return distinct
            .GroupBy(crossing => crossing.Line.Criterion)
            .SelectMany(group => PointClusters.ByStation(group, crossing => crossing.Station, rules.PointMergeDistance)
                .Select(cluster => ToHit(alignment.Name, group.Key, cluster)))
            .ToList();
    }

    // Only crossings inside the LER request area of the file the line came from count, as in CREATELERDATAPSS.
    private static Crossing[] CrossingsOf(Alignment alignment, ILer3dManager ler, AcEntity entity, LerLine line) =>
        Boundary.TryOption(() => alignment.IntersectWithValidation((Curve)entity, new List<Point3d>()))
            .OrElse(new List<Point3d>())
            .Where(point => Boundary.TryOption(() => ler.IsPointWithinPolygon(entity, point)).OrElse(false))
            .Select(point => AmkStations.StationOf(alignment, point).Map(station => new Crossing(line, station, point)))
            .Values()
            .ToArray();

    private static Hit ToHit(string alignmentName, LerCriterion criterion, IReadOnlyList<Crossing> cluster)
    {
        Crossing first = cluster[0];
        List<LerLine> lines = cluster.Select(crossing => crossing.Line).ToList();
        string owners = AmkTemplate.JoinDistinct(lines.Select(line => line.Owner), ", ");

        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [criterion.Measure.Placeholder] = AmkTemplate.JoinDistinct(lines.Select(line => criterion.Measure.Format(line.Value)), "/"),
            ["type"] = AmkTemplate.JoinDistinct(lines.Select(line => line.Type), ", "),
            ["ejer"] = owners,
            ["antal"] = cluster.Count.ToString(CultureInfo.InvariantCulture),
        };

        return new Hit(
            criterion.Crossing,
            alignmentName,
            HitExtent.Point(first.Station),
            values,
            new HitTrace(
                AmkTemplate.JoinDistinct(lines.Select(line => criterion.Measure.Detail(line.Value)), ", "),
                cluster.Count,
                owners,
                AmkTemplate.JoinDistinct(lines.Select(line => line.SourceFile), ", "),
                AmkTemplate.JoinDistinct(lines.Select(line => line.Handle), ", "),
                first.Point.X,
                first.Point.Y));
    }

    private static string SourceName(AcEntity entity) => Path.GetFileNameWithoutExtension(entity.Database.Filename);
}

/// <summary>LER lines that run alongside the trench, for the criteria where that is switched on.</summary>
internal sealed class AlongsideCheck
{
    private static readonly GeometryFactory Factory = new GeometryFactory();
    private readonly IReadOnlyList<(LerCriterion Criterion, SegmentIndex<LerLine> Lines)> _indexes;

    private AlongsideCheck(IReadOnlyList<(LerCriterion Criterion, SegmentIndex<LerLine> Lines)> indexes) => _indexes = indexes;

    public static AlongsideCheck Build(ILer3dManager ler, AmkRules rules, ICollection<string> warnings)
    {
        List<(LerCriterion Criterion, SegmentIndex<LerLine> Lines)> indexes = rules.LerCriteria
            .Where(criterion => criterion.AlongsideEnabled)
            .Select(criterion => (criterion, new SegmentIndex<LerLine>()))
            .ToList();
        if (indexes.Count == 0) return new AlongsideCheck(indexes);

        IEnumerable<Database> databases = Boundary.TryOption(() => (IEnumerable<Database>)ler.GetDatabases())
            .OrElse(Enumerable.Empty<Database>());
        foreach (Database database in databases)
        {
            Boundary.Try(() => AddDatabase(database, indexes), $"LER-filen {Path.GetFileName(database.Filename)} kunne ikke læses til 'langs med'")
                .FaultMessage()
                .Switch(warnings.Add, () => { });
        }
        return new AlongsideCheck(indexes);
    }

    private static bool AddDatabase(Database database, IReadOnlyList<(LerCriterion Criterion, SegmentIndex<LerLine> Lines)> indexes)
    {
        string source = Path.GetFileNameWithoutExtension(database.Filename);
        List<LerCriterion> criteria = indexes.Select(entry => entry.Criterion).ToList();

        using Transaction tx = database.TransactionManager.StartOpenCloseTransaction();
        Option<NtsGeometry> requestArea = database.ListOfType<MPolygon>(tx)
            .FirstOption()
            .Bind(area => Boundary.TryOption(() => (NtsGeometry)NTSConversion.ConvertMPolygonToNTSPolygon(area)));

        foreach (Curve curve in database.ListOfType<Curve>(tx))
        {
            LerClassifier.Classify(curve, criteria, source).Switch(
                line => CurveCoordinates.Of(curve, tx, 0.5).Switch(
                    coordinates =>
                    {
                        SegmentIndex<LerLine> index = indexes.First(entry => entry.Criterion == line.Criterion).Lines;
                        foreach (Coordinate[] part in InsideArea(coordinates, requestArea)) index.Add(part, line);
                    },
                    () => { }),
                () => { });
        }
        return true;
    }

    // A LER answer often draws lines past the requested area; only the part inside it is that file's to tell.
    private static IEnumerable<Coordinate[]> InsideArea(Coordinate[] line, Option<NtsGeometry> area) =>
        area.Match(
            polygon => Boundary.TryOption(() => polygon.Intersection(Factory.CreateLineString(line)))
                .Map(LineParts)
                .OrElse(new[] { line }),
            () => new[] { line });

    private static IEnumerable<Coordinate[]> LineParts(NtsGeometry geometry) =>
        Enumerable.Range(0, geometry.NumGeometries)
            .Select(geometry.GetGeometryN)
            .OfType<NtsLineString>()
            .Select(part => part.Coordinates)
            .Where(coordinates => coordinates.Length >= 2)
            .ToList();

    public IReadOnlyList<Hit> Run(string alignmentName, IReadOnlyList<StationSample> samples, TrenchWidthMap widths, AmkRules rules) =>
        _indexes
            .Where(entry => entry.Lines.Count > 0)
            .SelectMany(entry => HitsFor(entry.Criterion, entry.Lines, alignmentName, samples, widths, rules))
            .ToList();

    private static IEnumerable<Hit> HitsFor(
        LerCriterion criterion,
        SegmentIndex<LerLine> lines,
        string alignmentName,
        IReadOnlyList<StationSample> samples,
        TrenchWidthMap widths,
        AmkRules rules)
    {
        IEnumerable<(double Station, Option<(LerLine Line, StationSample Sample)> Flag)> flags = samples.Select(sample =>
            (sample.Station, widths.At(sample.Station).Bind(width =>
            {
                double reach = width / 2 + rules.AlongsideDistance;
                return lines.FirstTouching(sample.At(-reach), sample.At(reach)).Map(line => (line, sample));
            })));

        return StretchBuilder.Build(flags, rules.SampleStep, rules.StretchJoinGap, rules.AlongsideMinLength)
            .Select(stretch => ToHit(alignmentName, criterion, stretch));
    }

    private static Hit ToHit(string alignmentName, LerCriterion criterion, Stretch<(LerLine Line, StationSample Sample)> stretch)
    {
        List<LerLine> lines = stretch.Values.Select(value => value.Line).GroupBy(line => line.Id).Select(group => group.First()).ToList();
        StationSample first = stretch.Values[0].Sample;
        string owners = AmkTemplate.JoinDistinct(lines.Select(line => line.Owner), ", ");

        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [criterion.Measure.Placeholder] = AmkTemplate.JoinDistinct(lines.Select(line => criterion.Measure.Format(line.Value)), "/"),
            ["type"] = AmkTemplate.JoinDistinct(lines.Select(line => line.Type), ", "),
            ["ejer"] = owners,
            ["længde"] = AmkNumbers.Text(stretch.Length, "0.0"),
            ["antal"] = lines.Count.ToString(CultureInfo.InvariantCulture),
        };

        return new Hit(
            criterion.Alongside,
            alignmentName,
            HitExtent.Stretch(stretch.From, stretch.To),
            values,
            new HitTrace(
                $"{AmkTemplate.JoinDistinct(lines.Select(line => criterion.Measure.Detail(line.Value)), ", ")}, {AmkNumbers.Meters(stretch.Length)}",
                lines.Count,
                owners,
                AmkTemplate.JoinDistinct(lines.Select(line => line.SourceFile), ", "),
                AmkTemplate.JoinDistinct(lines.Select(line => line.Handle), ", "),
                first.Point.X,
                first.Point.Y));
    }
}

internal sealed record ProfilePair(Profile Surface, Profile Bottom);

/// <summary>The terrain and BUND profiles of every alignment in the Længdeprofiler drawings, kept open for sampling.</summary>
internal sealed class ProfileSet : IDisposable
{
    private readonly List<Transaction> _transactions;
    private readonly Dictionary<string, ProfilePair> _pairs;

    private ProfileSet(List<Transaction> transactions, Dictionary<string, ProfilePair> pairs)
    {
        _transactions = transactions;
        _pairs = pairs;
    }

    public static readonly ProfileSet Empty =
        new ProfileSet(new List<Transaction>(), new Dictionary<string, ProfilePair>(StringComparer.OrdinalIgnoreCase));

    public Option<ProfilePair> For(string alignmentName) => _pairs.Lookup(alignmentName);

    public static ProfileSet Open(IEnumerable<Database> databases, ICollection<string> warnings)
    {
        List<Transaction> transactions = new List<Transaction>();
        Dictionary<string, ProfilePair> pairs = new Dictionary<string, ProfilePair>(StringComparer.OrdinalIgnoreCase);

        foreach (Database database in databases)
        {
            Boundary.Try(() => AddDatabase(database, transactions, pairs), $"Længdeprofilerne i {Path.GetFileName(database.Filename)} kunne ikke læses")
                .FaultMessage()
                .Switch(warnings.Add, () => { });
        }
        return new ProfileSet(transactions, pairs);
    }

    private static bool AddDatabase(Database database, List<Transaction> transactions, Dictionary<string, ProfilePair> pairs)
    {
        Transaction tx = database.TransactionManager.StartTransaction();
        transactions.Add(tx);

        foreach (Alignment alignment in database.ListOfType<Alignment>(tx))
        {
            List<Profile> profiles = alignment.GetProfileIds()
                .Cast<ObjectId>()
                .Select(id => tx.GetObject(id, OpenMode.ForRead))
                .OfType<Profile>()
                .ToList();

            Option<Profile> surface = profiles.Where(profile => profile.Name.EndsWith("_surface_P", StringComparison.OrdinalIgnoreCase)).FirstOption();
            Option<Profile> bottom = profiles.Where(profile => profile.Name.EndsWith(" BUND", StringComparison.OrdinalIgnoreCase)).FirstOption();
            surface.Bind(terrain => bottom.Map(bund => new ProfilePair(terrain, bund)))
                .Switch(pair => pairs[alignment.Name] = pair, () => { });
        }
        return true;
    }

    public void Dispose()
    {
        foreach (Transaction tx in _transactions)
        {
            tx.Abort();
            tx.Dispose();
        }
        _transactions.Clear();
    }
}

internal static class DepthCheck
{
    public static IReadOnlyList<Hit> Run(string alignmentName, ProfileSet profiles, SampleLookup lookup, AmkRules rules, ICollection<string> warnings) =>
        profiles.For(alignmentName).Match(
            pair => Hits(alignmentName, pair, lookup, rules),
            () =>
            {
                warnings.Add($"Alignment {alignmentName}: ingen terræn- og BUND-profil i længdeprofilerne, dybden er ikke vurderet.");
                return (IReadOnlyList<Hit>)Array.Empty<Hit>();
            });

    private static IReadOnlyList<Hit> Hits(string alignmentName, ProfilePair pair, SampleLookup lookup, AmkRules rules)
    {
        double start = Math.Max(pair.Surface.StartingStation, pair.Bottom.StartingStation);
        double end = Math.Min(pair.Surface.EndingStation, pair.Bottom.EndingStation);
        int count = (int)Math.Floor((end - start) / rules.SampleStep + 1e-9);

        IEnumerable<(double Station, Option<double> Flag)> flags = Enumerable.Range(0, Math.Max(0, count + 1))
            .Select(i => start + i * rules.SampleStep)
            .Select(station => (station, DepthAt(pair, station, rules).Where(depth => depth > rules.DepthLimit + 1e-9)));

        return StretchBuilder.Build(flags, rules.SampleStep, rules.StretchJoinGap, 0.0)
            .Select(stretch => ToHit(alignmentName, stretch, lookup))
            .ToList();
    }

    // Depth to the bottom of the trench: terrain minus (pipe underside minus bedding).
    private static Option<double> DepthAt(ProfilePair pair, double station, AmkRules rules) =>
        Boundary.TryOption(() => pair.Surface.ElevationAt(station) - (pair.Bottom.ElevationAt(station) - rules.Bedding));

    private static Hit ToHit(string alignmentName, Stretch<double> stretch, SampleLookup lookup)
    {
        double deepest = stretch.Values.Max();
        Point2d at = lookup.Nearest(stretch.From).Match(sample => sample.Point, () => new Point2d(0, 0));
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["maks"] = AmkNumbers.Text(deepest, "0.00"),
            ["længde"] = AmkNumbers.Text(stretch.Length, "0.0"),
        };
        return new Hit(
            HitKind.Depth,
            alignmentName,
            HitExtent.Stretch(stretch.From, stretch.To),
            values,
            new HitTrace($"Maks. {AmkNumbers.Meters(deepest)} til underkant udgravning", 1, "", "Længdeprofiler", "", at.X, at.Y));
    }
}

internal static class NarrowRoadCheck
{
    private sealed record RoadSection(double FreeWidth, double RoadWidth, StationSample Sample);

    public static IReadOnlyList<Hit> Run(
        string alignmentName, IReadOnlyList<StationSample> samples, TrenchWidthMap widths, SegmentIndex<string> roadEdges, AmkRules rules)
    {
        IEnumerable<(double Station, Option<RoadSection> Flag)> flags = samples.Select(sample =>
            (sample.Station, widths.At(sample.Station)
                .Bind(width => Section(sample, width, roadEdges, rules))
                .Where(section => section.FreeWidth < rules.RoadMinFreeWidth - 1e-9)));

        return StretchBuilder.Build(flags, rules.SampleStep, rules.StretchJoinGap, rules.RoadMinStretchLength)
            .Select(stretch => ToHit(alignmentName, stretch))
            .ToList();
    }

    /// <summary>
    /// Distance from each trench edge out to the nearest road edge within the search distance, left and right of
    /// travel. The search starts at the trench edge: a kerb line inside the trench footprint is dug through and
    /// does not bound the traffic.
    /// </summary>
    public static (Option<double> Left, Option<double> Right) EdgeGaps(
        StationSample sample, double trenchWidth, SegmentIndex<string> roadEdges, AmkRules rules)
    {
        double half = trenchWidth / 2;
        double reach = half + rules.RoadSearchDistance;
        return (
            roadEdges.NearestDistanceAlong(sample.At(-half), sample.At(-reach)),
            roadEdges.NearestDistanceAlong(sample.At(half), sample.At(reach)));
    }

    // Traffic passes on the wider side; a station without a road edge on both sides is not in a road.
    private static Option<RoadSection> Section(StationSample sample, double trenchWidth, SegmentIndex<string> roadEdges, AmkRules rules)
    {
        (Option<double> left, Option<double> right) = EdgeGaps(sample, trenchWidth, roadEdges, rules);
        return left.Bind(gapLeft => right.Map(gapRight =>
            new RoadSection(
                Math.Max(Math.Max(gapLeft, gapRight) - rules.RoadBarrierAllowance, 0),
                gapLeft + trenchWidth + gapRight,
                sample)));
    }

    private static Hit ToHit(string alignmentName, Stretch<RoadSection> stretch)
    {
        double narrowest = stretch.Values.Min(section => section.FreeWidth);
        double minRoad = stretch.Values.Min(section => section.RoadWidth);
        double maxRoad = stretch.Values.Max(section => section.RoadWidth);
        StationSample first = stretch.Values[0].Sample;
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["min"] = AmkNumbers.Text(narrowest, "0.00"),
            ["længde"] = AmkNumbers.Text(stretch.Length, "0.0"),
        };
        return new Hit(
            HitKind.NarrowRoad,
            alignmentName,
            HitExtent.Stretch(stretch.From, stretch.To),
            values,
            new HitTrace(
                $"Fri bredde ned til {AmkNumbers.Meters(narrowest)}, vejbredde {AmkNumbers.Text(minRoad, "0.00")}-{AmkNumbers.Meters(maxRoad)}",
                1,
                "",
                "Grundkort (Vejkant)",
                "",
                first.Point.X,
                first.Point.Y));
    }
}

internal static class SoilCheck
{
    public static IReadOnlyList<Hit> Run(
        string alignmentName, IReadOnlyList<StationSample> samples, TrenchWidthMap widths, AreaIndex<string> areas, AmkRules rules) =>
        new[] { "V1", "V2" }
            .SelectMany(soilClass => HitsFor(soilClass, alignmentName, samples, widths, areas, rules))
            .ToList();

    private static IEnumerable<Hit> HitsFor(
        string soilClass, string alignmentName, IReadOnlyList<StationSample> samples, TrenchWidthMap widths, AreaIndex<string> areas, AmkRules rules)
    {
        IEnumerable<(double Station, Option<StationSample> Flag)> flags = samples.Select(sample =>
            (sample.Station, widths.At(sample.Station)
                .Where(width => areas.Touching(sample.At(-(width / 2 + rules.SoilMargin)), sample.At(width / 2 + rules.SoilMargin)).Contains(soilClass))
                .Map(_ => sample)));

        return StretchBuilder.Build(flags, rules.SampleStep, rules.StretchJoinGap, 0.0)
            .Select(stretch =>
            {
                StationSample first = stretch.Values[0];
                Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["klasse"] = soilClass,
                    ["længde"] = AmkNumbers.Text(stretch.Length, "0.0"),
                };
                return new Hit(
                    HitKind.Soil,
                    alignmentName,
                    HitExtent.Stretch(stretch.From, stretch.To),
                    values,
                    new HitTrace($"Kortlagt {soilClass}, {AmkNumbers.Meters(stretch.Length)}", 1, "", "DKjord", "", first.Point.X, first.Point.Y));
            });
    }
}

internal static class ValveCollector
{
    private sealed record FoundValve(ValveInfo Info, Option<string> Alignment, Point2d Position);

    public static IReadOnlyList<ValveLocation> Collect(
        Database fremtidDb,
        Transaction fremtidTx,
        IReadOnlyDictionary<string, Alignment> alignments,
        Transaction alignmentTx,
        AmkRules rules,
        ICollection<string> warnings)
    {
        IEnumerable<BlockReference> blocks = Boundary.Try(
                () => (IEnumerable<BlockReference>)fremtidDb.GetFjvBlocks(fremtidTx),
                "FJV-komponenterne i tegningen kunne ikke læses")
            .Match(
                found => found,
                fault =>
                {
                    warnings.Add(fault);
                    return Enumerable.Empty<BlockReference>();
                });

        List<FoundValve> valves = blocks.Select(block => Valve(block, rules)).Values().ToList();

        foreach (FoundValve valve in valves.Where(valve => !valve.Alignment.IsSome()))
        {
            warnings.Add($"Ventil {valve.Info.BlockName} ({valve.Info.Handle}) er ikke tilknyttet en alignment og står uden station i journalen.");
        }

        return valves
            .GroupBy(valve => valve.Alignment.OrElse(""))
            .SelectMany(group => PointClusters.ByPosition(group.ToList(), valve => valve.Position, rules.ValveMergeDistance))
            .Select(cluster => Location(cluster, alignments, alignmentTx))
            .OrderBy(location => location.Alignment.OrElse("~"), StringComparer.OrdinalIgnoreCase)
            .ThenBy(location => location.Station.OrElse(double.MaxValue))
            .ToList();
    }

    private static Option<FoundValve> Valve(BlockReference block, AmkRules rules) =>
        Boundary.TryOption(() => block.GetPipelineType())
            .Where(type => rules.ValveTypes.Contains(type.ToString()))
            .Map(type => new FoundValve(
                new ValveInfo(
                    Boundary.TryOption(() => block.RealName()).OrElse(block.Name),
                    block.Handle.ToString(),
                    Boundary.TryOption(() => block.ReadDynamicPropertyValue("Betegnelse")).Where(text => text.Length > 0).OrElse(type.ToString()),
                    Boundary.TryOption(() => block.ReadDynamicCsvProperty(DynamicProperty.DN1)).OrElse(""),
                    Boundary.TryOption(() => block.GetPipeTypeEnum().ToString()).OrElse(""),
                    type.ToString(),
                    block.Position.X,
                    block.Position.Y),
                PropertySetValues.AlignmentOf(block),
                new Point2d(block.Position.X, block.Position.Y)));

    private static ValveLocation Location(IReadOnlyList<FoundValve> cluster, IReadOnlyDictionary<string, Alignment> alignments, Transaction alignmentTx)
    {
        Option<string> alignmentName = cluster[0].Alignment;
        Option<double> station = alignmentName
            .Bind(name => alignments.Lookup(name))
            .Bind(alignment => cluster
                .Select(valve => AmkStations.StationOnAlignment(alignment, alignmentTx, valve.Position))
                .Values()
                .OrderBy(value => value)
                .FirstOption());
        return new ValveLocation(alignmentName, station, cluster.Select(valve => valve.Info).ToList());
    }
}

internal static class AmkStations
{
    /// <summary>Station of a point that lies on the alignment, such as a crossing point.</summary>
    public static Option<double> StationOf(Alignment alignment, Point3d point) =>
        Boundary.TryOption(() =>
        {
            double station = 0, offset = 0;
            alignment.StationOffset(point.X, point.Y, ref station, ref offset);
            return station;
        });

    /// <summary>
    /// Station of the closest point on the alignment. Projecting onto the alignment polyline first keeps a
    /// component just past an alignment end at that end, where StationOffset alone gives a wrong station.
    /// </summary>
    public static Option<double> StationOnAlignment(Alignment alignment, Transaction alignmentTx, Point2d position) =>
        Boundary.TryOption(() =>
        {
            Polyline polyline = (Polyline)alignmentTx.GetObject(alignment.GetPolyline(), OpenMode.ForWrite);
            Point3d closest = polyline.GetClosestPointTo(new Point3d(position.X, position.Y, 0), false);
            polyline.Erase(true);
            double station = 0, offset = 0;
            alignment.StationOffset(closest.X, closest.Y, ref station, ref offset);
            return station;
        });
}
