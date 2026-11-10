using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using AcColor = Autodesk.AutoCAD.Colors.Color;
using AcEntity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.MPE.AutoAmkB;

/// <summary>
/// The narrow-road measurement at one check sample: the trench width and the distance from each trench edge out to
/// the nearest road edge, where known. Montagehul B is not in it, so the map can apply any B to it.
/// </summary>
internal sealed record SampleSection(StationSample Sample, Option<double> TrenchWidth, Option<double> GapLeft, Option<double> GapRight);

/// <summary>
/// What the map needs of one alignment: its line, the 10 m station ticks, the measurement at every check sample (for
/// recomputing narrow roads with another B) and the indices of the samples the car-passage bands are drawn from.
/// </summary>
internal sealed record AlignmentTrace(
    string Name,
    IReadOnlyList<StationSample> Path,
    IReadOnlyList<StationSample> Ticks,
    IReadOnlyList<SampleSection> Sections,
    IReadOnlyList<int> PassageIndices)
{
    public IEnumerable<SampleSection> Passage => PassageIndices.Select(index => Sections[index]).Where(section => section.TrenchWidth.IsSome());

    /// <summary>The plan point at a station, interpolated along the path and held at its ends.</summary>
    public Option<Point2d> At(double station)
    {
        if (Path.Count == 0) return Option<Point2d>.Nothing;
        if (station <= Path[0].Station) return Option<Point2d>.Of(Path[0].Point);
        if (station >= Path[^1].Station) return Option<Point2d>.Of(Path[^1].Point);

        int low = 1, high = Path.Count - 1;
        while (low < high)
        {
            int middle = (low + high) / 2;
            if (Path[middle].Station < station) low = middle + 1;
            else high = middle;
        }
        StationSample before = Path[low - 1], after = Path[low];
        double t = (station - before.Station) / (after.Station - before.Station);
        return Option<Point2d>.Of(before.Point + (after.Point - before.Point) * t);
    }

    /// <summary>The path from one station to another, both ends included.</summary>
    public IReadOnlyList<Point2d> Between(double from, double to) =>
        new[] { At(from) }
            .Concat(Path.Where(sample => sample.Station > from && sample.Station < to).Select(sample => Option<Point2d>.Of(sample.Point)))
            .Append(At(to))
            .Values()
            .ToList();
}

internal static class AlignmentTraces
{
    public const double PassageStep = 0.5;
    private const double PathStep = 1.0;
    private const double PathTurn = 2.0 * Math.PI / 180.0;
    private const double NoTurnLimit = Math.PI;
    private const double TickStep = 10.0;

    /// <summary>
    /// Measures every check sample with the same road-edge search as the narrow-road check, so the map can redo the
    /// check with another B and get the same stretches, and thins the samples for drawing.
    /// </summary>
    public static AlignmentTrace Build(
        Alignment alignment, IReadOnlyList<StationSample> samples, TrenchWidthMap widths, Option<SegmentIndex<string>> roadEdges, AmkRules rules)
    {
        List<SampleSection> sections = samples.Select(sample => Section(sample, widths, roadEdges, rules)).ToList();

        return new AlignmentTrace(
            alignment.Name,
            Thin(samples, PathStep, PathTurn).Select(index => samples[index]).ToList(),
            Boundary.TryOption(() => Ticks(alignment)).OrElse(Array.Empty<StationSample>()),
            sections,
            Thin(samples, PassageStep, NoTurnLimit));
    }

    private static SampleSection Section(StationSample sample, TrenchWidthMap widths, Option<SegmentIndex<string>> roadEdges, AmkRules rules)
    {
        Option<double> width = widths.At(sample.Station);
        (Option<double> left, Option<double> right) = width
            .Bind(trenchWidth => roadEdges.Map(edges => NarrowRoadCheck.EdgeGaps(sample, trenchWidth, edges, rules)))
            .OrElse((Option<double>.Nothing, Option<double>.Nothing));
        return new SampleSection(sample, width, left, right);
    }

    private static IReadOnlyList<StationSample> Ticks(Alignment alignment)
    {
        double first = Math.Ceiling(alignment.StartingStation / TickStep - 1e-9) * TickStep;
        int count = (int)Math.Floor((alignment.EndingStation - first) / TickStep + 1e-9) + 1;
        return Enumerable.Range(0, Math.Max(0, count))
            .Select(i => AlignmentSampler.SampleAt(alignment, first + i * TickStep))
            .Values()
            .ToList();
    }

    // The indices of the first sample, then each sample one step past the last one kept or turned more than maxTurn
    // from it, and the last sample. The turn rule keeps tight bends on the line, so findings in a bend sit on it.
    private static IReadOnlyList<int> Thin(IReadOnlyList<StationSample> samples, double step, double maxTurn)
    {
        List<int> kept = new List<int>();
        for (int i = 0; i < samples.Count; i++)
        {
            bool keep = kept.Count == 0
                || samples[i].Station - samples[kept[^1]].Station >= step - 1e-6
                || Turn(samples[kept[^1]].Right, samples[i].Right) > maxTurn;
            if (keep) kept.Add(i);
        }
        if (kept.Count > 0 && kept[^1] < samples.Count - 1) kept.Add(samples.Count - 1);
        return kept;
    }

    private static double Turn(Vector2d from, Vector2d to) =>
        from.Length < 1e-9 || to.Length < 1e-9 ? 0.0 : Math.Acos(Math.Clamp(from.DotProduct(to) / (from.Length * to.Length), -1.0, 1.0));
}

/// <summary>The ground within roughly 40-80 m of the alignments; only drawing parts there go on the map.</summary>
internal sealed class MapCorridor
{
    private const double Cell = 40.0;
    private readonly HashSet<(long X, long Y)> _cells;

    public MapCorridor(IEnumerable<Point2d> path) => _cells = path.Select(point => CellOf(point.X, point.Y)).ToHashSet();

    public bool Near(double x, double y)
    {
        (long cellX, long cellY) = CellOf(x, y);
        for (long dx = -1; dx <= 1; dx++)
        {
            for (long dy = -1; dy <= 1; dy++)
            {
                if (_cells.Contains((cellX + dx, cellY + dy))) return true;
            }
        }
        return false;
    }

    private static (long X, long Y) CellOf(double x, double y) => ((long)Math.Floor(x / Cell), (long)Math.Floor(y / Cell));
}

internal sealed record MapText(Point2d Position, double Height, double Rotation, string Text);

/// <summary>A run of a drawn line, and the LER data of the line it belongs to (an index into the map's list).</summary>
internal sealed record MapLine(IReadOnlyList<Point2d> Points, Option<int> Info);

/// <summary>One drawing layer on the map: its colour, its lines as point runs and its texts.</summary>
internal sealed class MapLayer
{
    public MapLayer(string name, string colour)
    {
        Name = name;
        Colour = colour;
    }

    public string Name { get; }

    public string Colour { get; }

    public List<MapLine> Lines { get; } = new List<MapLine>();

    public List<MapText> Texts { get; } = new List<MapText>();
}

/// <summary>
/// The drawing as read for the map, with each distinct LER line description listed once and the blocks that show
/// their data when clicked.
/// </summary>
internal sealed record MapBackground(IReadOnlyList<MapLayer> Layers, IReadOnlyList<LerInfo> LerInfos, IReadOnlyList<MapBlock> Blocks);

internal sealed record InfoField(string Label, string Value);

/// <summary>What the map shows when a LER line is clicked: its kind and the fields useful in a review.</summary>
internal sealed record LerInfo(string Kind, IReadOnlyList<InfoField> Fields)
{
    public string Key => Kind + "\u001f" + string.Join("\u001f", Fields.Select(field => field.Label + "\u001e" + field.Value));
}

/// <summary>
/// A block that shows its data when clicked on the map: its name, the box around what the map draws of it, the
/// layers that is drawn on, its lines (which the map highlights when the block is selected) and its fields.
/// </summary>
internal sealed record MapBlock(
    string Name,
    Point2d Min,
    Point2d Max,
    IReadOnlyList<string> Layers,
    IReadOnlyList<MapBlockLines> Lines,
    IReadOnlyList<InfoField> Fields);

/// <summary>Some of a block's lines: Count lines of a layer, from the layer's line number First.</summary>
internal sealed record MapBlockLines(string Layer, int First, int Count);

/// <summary>Grows around what the map draws of one block, and notes its lines and the layers it is drawn on.</summary>
internal sealed class BlockOutline
{
    private readonly HashSet<string> _layers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<MapBlockLines> _lines = new List<MapBlockLines>();
    private double _minX = double.MaxValue, _minY = double.MaxValue, _maxX = double.MinValue, _maxY = double.MinValue;

    public bool IsEmpty => _layers.Count == 0;

    public Point2d Min => new Point2d(_minX, _minY);

    public Point2d Max => new Point2d(_maxX, _maxY);

    public IReadOnlyList<string> Layers => _layers.ToList();

    public IReadOnlyList<MapBlockLines> Lines => _lines;

    /// <summary>
    /// Takes in lines just added to a layer, the first of them as line number first there. A block is read in one
    /// go, so its lines on a layer follow each other and mostly make one stretch.
    /// </summary>
    public void IncludeLines(string layer, int first, IReadOnlyList<IReadOnlyList<Point2d>> runs)
    {
        Include(layer, runs.SelectMany(run => run));

        int last = _lines.FindLastIndex(lines => lines.Layer.Equals(layer, StringComparison.OrdinalIgnoreCase));
        if (last >= 0 && _lines[last].First + _lines[last].Count == first)
        {
            _lines[last] = _lines[last] with { Count = _lines[last].Count + runs.Count };
        }
        else
        {
            _lines.Add(new MapBlockLines(layer, first, runs.Count));
        }
    }

    public void Include(string layer, IEnumerable<Point2d> points)
    {
        foreach (Point2d point in points)
        {
            _layers.Add(layer);
            _minX = Math.Min(_minX, point.X);
            _minY = Math.Min(_minY, point.Y);
            _maxX = Math.Max(_maxX, point.X);
            _maxY = Math.Max(_maxY, point.Y);
        }
    }
}

/// <summary>
/// Reads what the map shows for a clicked block: its attribute values, a component's designation (the dynamic
/// property Betegnelse) and the alignment it is assigned to. A block with none of these gets no data.
/// </summary>
internal static class BlockInfoReader
{
    private const string DesignationProperty = "Betegnelse";

    public static IReadOnlyList<InfoField> Read(BlockReference reference, IEnumerable<AttributeReference> attributes) =>
        attributes
            .Select(attribute => Option<InfoField>.Of(new InfoField(Label(attribute.Tag), OneLine(attribute.TextString))))
            .Append(Designation(reference).Map(text => new InfoField(DesignationProperty, OneLine(text))))
            .Append(Alignment(reference).Map(name => new InfoField("Alignment", name)))
            .Values()
            .Where(field => field.Value.Length > 0)
            .ToList();

    // Tags are written for the drafter: "TYPE", "Vinkel_1".
    private static string Label(string tag)
    {
        string words = tag.Replace('_', ' ').Trim();
        bool shouting = words.Length > 1 && words.Any(char.IsLetter) && !words.Any(char.IsLower);
        return shouting ? words.Substring(0, 1) + words.Substring(1).ToLowerInvariant() : words;
    }

    private static string OneLine(string text) =>
        string.Join(" ", text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));

    private static Option<string> Designation(BlockReference reference) =>
        reference.IsDynamicBlock
            ? Boundary.TryOption(() => reference.ReadDynamicPropertyValue(DesignationProperty)).Bind(text => AmkNullable.Of(text))
            : Option<string>.Nothing;

    // Property sets live in the extension dictionary, so a block without one is skipped without opening anything.
    private static Option<string> Alignment(BlockReference reference) =>
        reference.ExtensionDictionary.IsNull ? Option<string>.Nothing : PropertySetValues.AlignmentOf(reference);
}

/// <summary>One line of a text as the map draws it: where it starts, on its baseline, and which way it runs.</summary>
internal sealed record TextLine(Point3d Start, double Height, Vector3d Direction, string Text);

/// <summary>
/// The lines of a multiline text where AutoCAD lays them out, so wrapped, centred and stacked text sits on the map
/// as in the drawing. The text of a multiline attribute is not in the database and AutoCAD gives no layout for it;
/// its lines are then placed from its attachment point, each as wide as its share of the longest line.
/// </summary>
internal static class MTextLines
{
    // AutoCAD's line pitch at line spacing factor 1, in text heights.
    private const double LinePitch = 5.0 / 3.0;

    public static IReadOnlyList<TextLine> Of(MText text)
    {
        List<TextLine> lines = new List<TextLine>();
        text.ExplodeFragments((fragment, _) =>
        {
            lines.Add(new TextLine(fragment.Location, fragment.CapsHeight, fragment.Direction, fragment.Text));
            return MTextFragmentCallbackStatus.Continue;
        });
        return lines.Count > 0 ? lines : Estimated(text);
    }

    private static IReadOnlyList<TextLine> Estimated(MText text)
    {
        string[] rows = text.Text.Split('\n').Select(row => row.Trim()).ToArray();
        int longest = rows.Max(row => row.Length);
        if (longest == 0) return Array.Empty<TextLine>();

        Vector3d along = Vector3d.XAxis.RotateBy(text.Rotation, Vector3d.ZAxis);
        Vector3d up = Vector3d.YAxis.RotateBy(text.Rotation, Vector3d.ZAxis);
        double pitch = text.TextHeight * LinePitch * text.LineSpacingFactor;
        double height = text.TextHeight + (rows.Length - 1) * pitch;
        (double left, double above) = Anchor(text.Attachment);

        return rows
            .Select((row, index) => new TextLine(
                text.Location
                    - along * (left * text.ActualWidth * row.Length / longest)
                    + up * (above * height - text.TextHeight - index * pitch),
                text.TextHeight,
                along,
                row))
            .Where(line => line.Text.Length > 0)
            .ToList();
    }

    // The share of a line's width left of the attachment point, and of the text's height above it.
    private static (double Left, double Above) Anchor(AttachmentPoint attachment) => attachment switch
    {
        AttachmentPoint.TopCenter => (0.5, 0.0),
        AttachmentPoint.TopRight => (1.0, 0.0),
        AttachmentPoint.MiddleLeft => (0.0, 0.5),
        AttachmentPoint.MiddleCenter => (0.5, 0.5),
        AttachmentPoint.MiddleRight => (1.0, 0.5),
        AttachmentPoint.BottomLeft => (0.0, 1.0),
        AttachmentPoint.BottomCenter => (0.5, 1.0),
        AttachmentPoint.BottomRight => (1.0, 1.0),
        _ => (0.0, 0.0),
    };
}

/// <summary>
/// Reads the LER data of a line: any property set with an owner field, as LERImporter writes them. Unknown values
/// ("" and "0") are left out. Cable traces (Ledningstrace) carry little worth showing, so they get no info.
/// </summary>
internal static class LerInfoReader
{
    private const string OwnerProperty = "LedningsEjersNavn";
    private const string TraceSet = "Ledningstrace";

    private sealed record FieldSpec(string Property, string Label, Option<string> UnitProperty);

    private static readonly IReadOnlyList<FieldSpec> Specs = new[]
    {
        new FieldSpec("Type", "Type", Option<string>.Nothing),
        new FieldSpec(OwnerProperty, "Ejer", Option<string>.Nothing),
        new FieldSpec("Driftsstatus", "Driftsstatus", Option<string>.Nothing),
        new FieldSpec("SpændingsNiveau", "Spænding", Option<string>.Nothing),
        new FieldSpec("UdvendigDiameter", "Udv. diameter", Option<string>.Of("UdvendigDiameterUnits")),
        new FieldSpec("Tryk", "Tryk", Option<string>.Nothing),
        new FieldSpec("UdvendigMateriale", "Materiale", Option<string>.Nothing),
        new FieldSpec("KabelType", "Kabeltype", Option<string>.Nothing),
        new FieldSpec("AntalKabler", "Antal kabler", Option<string>.Nothing),
        new FieldSpec("Niveau", "Niveau", Option<string>.Nothing),
        new FieldSpec("VejledendeDybde", "Vejl. dybde (m)", Option<string>.Nothing),
        new FieldSpec("Fareklasse", "Fareklasse", Option<string>.Nothing),
        new FieldSpec("EtableringsTidspunkt", "Etableret", Option<string>.Nothing),
        new FieldSpec("Nøjagtighedsklasse", "Nøjagtighed", Option<string>.Nothing),
        new FieldSpec("LerNummer", "LER-nr.", Option<string>.Nothing),
    };

    private static readonly IReadOnlyDictionary<string, string> KindNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Afloebsledning"] = "Afløbsledning",
        ["TermiskLedning"] = "Termisk ledning",
    };

    // Property sets live in the extension dictionary, so a line without one is skipped without opening anything.
    public static Option<LerInfo> Read(AcEntity entity) =>
        entity.ExtensionDictionary.IsNull
            ? Option<LerInfo>.Nothing
            : PropertySetValues.Read(entity)
                .Where(set => !set.Key.Equals(TraceSet, StringComparison.OrdinalIgnoreCase) && set.Value.ContainsKey(OwnerProperty))
                .Select(set => new LerInfo(
                    KindNames.Lookup(set.Key).OrElse(set.Key),
                    Specs.Select(spec => Field(set.Value, spec)).Values().ToList()))
                .FirstOption();

    private static Option<InfoField> Field(IReadOnlyDictionary<string, string> values, FieldSpec spec) =>
        values.Lookup(spec.Property)
            .Map(value => value.Trim())
            .Where(value => value.Length > 0 && value != "0")
            .Map(value => new InfoField(
                spec.Label,
                spec.UnitProperty
                    .Bind(unit => values.Lookup(unit))
                    .Map(unit => unit.Trim())
                    .Where(unit => unit.Length > 0)
                    .Match(unit => $"{value} {unit}", () => value)));
}

/// <summary>
/// Reads what is visible in the drawing and its xrefs near the alignments, as the map's background. Visibility
/// comes from the drawing's own layer table, so a layer that is off or frozen there stays off on the map.
/// Entities inside an xref already carry its prefix ("Grundkort|Vejkant"). Hatches, points, dimensions and
/// leaders are left out. A block shows the attribute values of each of its references, and the references with
/// something to tell are listed so the map can show it when one is clicked.
/// </summary>
internal sealed class MapDrawing
{
    private const int MaxNesting = 6;
    private const int MaxTextLength = 80;
    private const double CorridorTestStep = 10.0;

    // A text's length is not measured; this share of its height per character is close enough for a block's box.
    private const double WidthPerCharacter = 0.6;

    private sealed record LayerLook(bool Off, bool Frozen, AcColor Color);

    private readonly Transaction _tx;
    private readonly MapCorridor _corridor;
    private readonly IReadOnlyDictionary<string, LayerLook> _hostLayers;
    private readonly Dictionary<string, MapLayer> _layers = new Dictionary<string, MapLayer>(StringComparer.OrdinalIgnoreCase);
    private readonly List<LerInfo> _lerInfos = new List<LerInfo>();
    private readonly Dictionary<string, int> _lerInfoIndex = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly List<MapBlock> _blocks = new List<MapBlock>();

    private MapDrawing(Transaction tx, MapCorridor corridor, IReadOnlyDictionary<string, LayerLook> hostLayers)
    {
        _tx = tx;
        _corridor = corridor;
        _hostLayers = hostLayers;
    }

    public static Result<MapBackground> Read(Database db, MapCorridor corridor) =>
        Boundary.Try(
            () =>
            {
                using Transaction tx = db.TransactionManager.StartOpenCloseTransaction();
                MapDrawing reader = new MapDrawing(tx, corridor, HostLayers(db, tx));
                BlockTable blocks = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tx.GetObject(blocks[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                reader.Walk(modelSpace, Matrix3d.Identity, "0", Option<AcColor>.Nothing, 0, Option<BlockOutline>.Nothing);
                return new MapBackground(
                    reader._layers.Values.OrderBy(layer => layer.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                    reader._lerInfos,
                    reader._blocks);
            },
            "Tegningen kunne ikke læses til kortet");

    private static IReadOnlyDictionary<string, LayerLook> HostLayers(Database db, Transaction tx)
    {
        Dictionary<string, LayerLook> looks = new Dictionary<string, LayerLook>(StringComparer.OrdinalIgnoreCase);
        LayerTable table = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
        foreach (ObjectId id in table)
        {
            LayerTableRecord layer = (LayerTableRecord)tx.GetObject(id, OpenMode.ForRead);
            looks[layer.Name] = new LayerLook(layer.IsOff, layer.IsFrozen, layer.Color);
        }
        return looks;
    }

    // As in AutoCAD, an entity on layer 0 inside a block takes the block's layer, and ByBlock takes the block's colour.
    // Some component blocks hold parts with no layer at all; those are treated as on layer 0.
    private void Walk(
        BlockTableRecord block, Matrix3d transform, string parentLayer, Option<AcColor> parentColor, int depth, Option<BlockOutline> outline)
    {
        if (depth > MaxNesting) return;

        IEnumerable<AcEntity> entities = block.Cast<ObjectId>()
            .Select(id => Boundary.TryOption(() => _tx.GetObject(id, OpenMode.ForRead)))
            .Values()
            .OfType<AcEntity>()
            .Where(entity => entity.Visible);

        foreach (AcEntity entity in entities) Place(entity, transform, parentLayer, parentColor, depth, outline);
    }

    private void Place(
        AcEntity entity, Matrix3d transform, string parentLayer, Option<AcColor> parentColor, int depth, Option<BlockOutline> outline)
    {
        string layer = entity.Layer.Length == 0 || entity.Layer == "0" ? parentLayer : entity.Layer;
        LayerLook look = Look(entity, layer);
        AcColor color = entity.Color.IsByLayer ? look.Color : entity.Color.IsByBlock ? parentColor.OrElse(look.Color) : entity.Color;
        Boundary.TryOption(() => Add(entity, transform, layer, look, color, depth, outline));
    }

    private LayerLook Look(AcEntity entity, string layer) =>
        _hostLayers.Lookup(layer).Match(
            look => look,
            () => Boundary.TryOption(() => (LayerTableRecord)_tx.GetObject(entity.LayerId, OpenMode.ForRead))
                .Map(own => new LayerLook(own.IsOff, own.IsFrozen, own.Color))
                .OrElse(new LayerLook(false, false, AcColor.FromColorIndex(ColorMethod.ByAci, 7))));

    // AutoCAD draws an attribute from the reference, which holds its value; the definition inside the block only
    // holds the prompt text, and is drawn only when it is constant.
    private bool Add(
        AcEntity entity, Matrix3d transform, string layer, LayerLook look, AcColor color, int depth, Option<BlockOutline> outline)
    {
        bool shown = !look.Off && !look.Frozen;
        switch (entity)
        {
            case BlockReference reference when !look.Frozen:
                AddBlock(reference, transform, layer, color, depth, outline);
                break;
            case Ray or Xline:
                break;
            case Curve curve when shown:
                AddLine(layer, color, CurvePoints(curve).Select(point => point.TransformBy(transform)).ToList(), curve, outline);
                break;
            case AttributeDefinition definition when !definition.Constant || definition.Invisible:
                break;
            case AttributeReference attribute when attribute.Invisible:
                break;
            case AttributeReference attribute when shown && attribute.IsMTextAttribute:
                using (MText content = attribute.MTextAttribute)
                {
                    AddMText(layer, color, content, transform, outline);
                }
                break;
            case DBText text when shown:
                AddText(layer, color, new TextLine(text.Position, text.Height, Along(text.Rotation), text.TextString), transform, outline);
                break;
            case MText text when shown:
                AddMText(layer, color, text, transform, outline);
                break;
        }
        return true;
    }

    private static Vector3d Along(double rotation) => Vector3d.XAxis.RotateBy(rotation, Vector3d.ZAxis);

    // The outermost block that is not an xref is the one a click selects; blocks nested in it are part of it. Its
    // attributes are stored in the space the reference is in, so they are placed there and not inside the block.
    private void AddBlock(
        BlockReference reference, Matrix3d transform, string layer, AcColor color, int depth, Option<BlockOutline> enclosing)
    {
        BlockTableRecord definition = (BlockTableRecord)_tx.GetObject(reference.BlockTableRecord, OpenMode.ForRead);
        bool xref = definition.IsFromExternalReference || definition.IsFromOverlayReference;
        if (xref && !definition.IsResolved) return;

        Option<BlockOutline> own = xref || enclosing.IsSome() ? Option<BlockOutline>.Nothing : Option<BlockOutline>.Of(new BlockOutline());
        Option<BlockOutline> outline = enclosing.IsSome() ? enclosing : own;

        Walk(definition, transform * reference.BlockTransform, layer, Option<AcColor>.Of(color), depth + 1, outline);

        List<AttributeReference> attributes = reference.AttributeCollection.Cast<ObjectId>()
            .Select(id => Boundary.TryOption(() => _tx.GetObject(id, OpenMode.ForRead)))
            .Values()
            .OfType<AttributeReference>()
            .ToList();
        foreach (AttributeReference attribute in attributes.Where(attribute => attribute.Visible))
        {
            Place(attribute, transform, layer, Option<AcColor>.Of(color), depth + 1, outline);
        }

        own.Where(drawn => !drawn.IsEmpty).Switch(drawn => AddBlockInfo(reference, definition, attributes, drawn), () => { });
    }

    private void AddBlockInfo(
        BlockReference reference, BlockTableRecord definition, IReadOnlyList<AttributeReference> attributes, BlockOutline drawn)
    {
        IReadOnlyList<InfoField> fields = BlockInfoReader.Read(reference, attributes);
        if (fields.Count == 0) return;

        // A dynamic block's reference points at an anonymous copy ("*U877"); the name is on the block it was made from.
        string name = reference.IsDynamicBlock
            ? ((BlockTableRecord)_tx.GetObject(reference.DynamicBlockTableRecord, OpenMode.ForRead)).Name
            : definition.Name;
        _blocks.Add(new MapBlock(name, drawn.Min, drawn.Max, drawn.Layers, drawn.Lines, fields));
    }

    private void AddMText(string layer, AcColor color, MText text, Matrix3d transform, Option<BlockOutline> outline)
    {
        Point3d at = text.Location.TransformBy(transform);
        if (!_corridor.Near(at.X, at.Y)) return;

        foreach (TextLine line in MTextLines.Of(text)) AddText(layer, color, line, transform, outline);
    }

    private IReadOnlyList<Point3d> CurvePoints(Curve curve) => curve switch
    {
        Line line => new[] { line.StartPoint, line.EndPoint },
        Polyline polyline => PolylinePoints(polyline),
        Polyline2d polyline when polyline.PolyType == Poly2dType.SimplePoly => Vertices(polyline),
        Polyline3d polyline when polyline.PolyType == Poly3dType.SimplePoly => Vertices(polyline),
        _ => Sampled(curve),
    };

    private static IReadOnlyList<Point3d> PolylinePoints(Polyline polyline)
    {
        const int ArcDivisions = 8;
        int vertices = polyline.NumberOfVertices;
        int segments = polyline.Closed ? vertices : vertices - 1;
        List<Point3d> points = new List<Point3d>();
        for (int i = 0; i < vertices; i++)
        {
            points.Add(polyline.GetPoint3dAt(i));
            if (i < segments && Math.Abs(polyline.GetBulgeAt(i)) > 1e-9)
            {
                for (int k = 1; k < ArcDivisions; k++) points.Add(polyline.GetPointAtParameter(i + (double)k / ArcDivisions));
            }
        }
        if (polyline.Closed && vertices > 0) points.Add(polyline.GetPoint3dAt(0));
        return points;
    }

    private IReadOnlyList<Point3d> Vertices(Polyline2d polyline)
    {
        List<Point3d> points = new List<Point3d>();
        foreach (ObjectId id in polyline) points.Add(polyline.VertexPosition((Vertex2d)_tx.GetObject(id, OpenMode.ForRead)));
        if (polyline.Closed && points.Count > 0) points.Add(points[0]);
        return points;
    }

    private IReadOnlyList<Point3d> Vertices(Polyline3d polyline)
    {
        List<Point3d> points = new List<Point3d>();
        foreach (ObjectId id in polyline) points.Add(((PolylineVertex3d)_tx.GetObject(id, OpenMode.ForRead)).Position);
        if (polyline.Closed && points.Count > 0) points.Add(points[0]);
        return points;
    }

    private static IReadOnlyList<Point3d> Sampled(Curve curve)
    {
        double length = curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam);
        int count = Math.Clamp((int)Math.Ceiling(length / 0.5), 8, 200);
        return Enumerable.Range(0, count + 1)
            .Select(k => curve.GetPointAtDist(Math.Min(length, length * k / count)))
            .ToList();
    }

    // Keeps the runs of segments that come near an alignment, testing every 10 m along each segment. The LER data
    // is read only for lines that made it onto the map.
    private void AddLine(string layer, AcColor color, IReadOnlyList<Point3d> points, Curve curve, Option<BlockOutline> outline)
    {
        List<IReadOnlyList<Point2d>> runs = new List<IReadOnlyList<Point2d>>();
        List<Point2d> run = new List<Point2d>();
        for (int i = 0; i + 1 < points.Count; i++)
        {
            Point3d a = points[i], b = points[i + 1];
            if (SegmentNear(a, b))
            {
                if (run.Count == 0) run.Add(new Point2d(a.X, a.Y));
                run.Add(new Point2d(b.X, b.Y));
            }
            else if (run.Count > 0)
            {
                runs.Add(run);
                run = new List<Point2d>();
            }
        }
        if (run.Count > 0) runs.Add(run);
        if (runs.Count == 0) return;

        Option<int> info = LerInfoReader.Read(curve).Map(IndexOf);
        MapLayer target = LayerFor(layer, color);
        int first = target.Lines.Count;
        target.Lines.AddRange(runs.Select(part => new MapLine(part, info)));
        outline.Switch(drawn => drawn.IncludeLines(layer, first, runs), () => { });
    }

    private int IndexOf(LerInfo info)
    {
        string key = info.Key;
        if (!_lerInfoIndex.TryGetValue(key, out int index))
        {
            index = _lerInfos.Count;
            _lerInfos.Add(info);
            _lerInfoIndex[key] = index;
        }
        return index;
    }

    private bool SegmentNear(Point3d a, Point3d b)
    {
        int steps = Math.Max(1, (int)Math.Ceiling(a.DistanceTo(b) / CorridorTestStep));
        for (int k = 0; k <= steps; k++)
        {
            if (_corridor.Near(a.X + (b.X - a.X) * k / steps, a.Y + (b.Y - a.Y) * k / steps)) return true;
        }
        return false;
    }

    private void AddText(string layer, AcColor color, TextLine line, Matrix3d transform, Option<BlockOutline> outline)
    {
        Point3d at = line.Start.TransformBy(transform);
        string trimmed = line.Text.Trim();
        Vector3d along = line.Direction.TransformBy(transform);
        if (trimmed.Length == 0 || along.Length < 1e-9 || !_corridor.Near(at.X, at.Y)) return;

        string text = trimmed.Length > MaxTextLength ? trimmed.Substring(0, MaxTextLength) : trimmed;
        double height = line.Height * along.Length / line.Direction.Length;
        Point2d start = new Point2d(at.X, at.Y);
        LayerFor(layer, color).Texts.Add(new MapText(start, height, Math.Atan2(along.Y, along.X), text));

        outline.Switch(
            drawn =>
            {
                Vector2d run = new Vector2d(along.X, along.Y).GetNormal() * (height * WidthPerCharacter * text.Length);
                Vector2d rise = run.GetPerpendicularVector().GetNormal() * height;
                drawn.Include(layer, new[] { start, start + run, start + rise, start + run + rise });
            },
            () => { });
    }

    // A layer takes the colour of the first entity found on it.
    private MapLayer LayerFor(string name, AcColor color)
    {
        if (!_layers.TryGetValue(name, out MapLayer? layer))
        {
            var value = color.ColorValue;
            layer = new MapLayer(name, $"#{value.R:X2}{value.G:X2}{value.B:X2}");
            _layers[name] = layer;
        }
        return layer;
    }
}

/// <summary>The review saved in a map: its state block as JSON, and what it holds, for the run summary.</summary>
internal sealed record SavedState(string Json, int Marks, int Overrides, Option<double> GlobalB, string SavedAt);

/// <summary>What a rerun finds where it writes the map: no map, a map with its saved state, or a file it cannot read.</summary>
internal abstract record PreviousMap
{
    private PreviousMap() { }

    public abstract TOut Match<TOut>(Func<TOut> none, Func<SavedState, TOut> saved, Func<string, TOut> unreadable);

    private static readonly Regex StateBlock = new Regex(
        "<script type=\"application/json\" id=\"state\">(.*?)</script>", RegexOptions.Singleline | RegexOptions.Compiled);

    public static PreviousMap Read(string path) =>
        !Boundary.TryOption(() => File.Exists(path)).OrElse(false)
            ? new NoMap()
            : Boundary.Try(() => File.ReadAllText(path, Encoding.UTF8), "Filen kunne ikke åbnes")
                .Bind(StateOf)
                .Match<PreviousMap>(state => new Saved(state), reason => new Unreadable(reason));

    // The page writes its state as JSON with < escaped, so the block cannot end early. It is parsed and written again,
    // which keeps anything else out of the new page.
    private static Result<SavedState> StateOf(string html)
    {
        System.Text.RegularExpressions.Match block = StateBlock.Match(html);
        if (!block.Success) return Result<SavedState>.Failure("Filen er ikke et AUTOAMKB-kort med gemt opsætning");

        return Boundary.Try(
                () =>
                {
                    using JsonDocument document = JsonDocument.Parse(block.Groups[1].Value);
                    JsonElement root = document.RootElement.Clone();
                    return root;
                },
                "Kortets gemte opsætning kan ikke læses")
            .Bind(root => root.ValueKind == JsonValueKind.Object
                ? Result<SavedState>.Success(new SavedState(
                    JsonSerializer.Serialize(root),
                    Count(root, "marks"),
                    Count(root, "overrides"),
                    root.TryGetProperty("globalB", out JsonElement global) && global.ValueKind == JsonValueKind.Number
                        ? Option<double>.Of(global.GetDouble())
                        : Option<double>.Nothing,
                    root.TryGetProperty("savedAt", out JsonElement savedAt) && savedAt.ValueKind == JsonValueKind.String
                        ? savedAt.GetString() ?? ""
                        : ""))
                : Result<SavedState>.Failure("Kortets gemte opsætning er ikke gyldig"));
    }

    private static int Count(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Object ? value.EnumerateObject().Count() : 0;

    internal sealed record NoMap : PreviousMap
    {
        public override TOut Match<TOut>(Func<TOut> none, Func<SavedState, TOut> saved, Func<string, TOut> unreadable) => none();
    }

    internal sealed record Saved(SavedState State) : PreviousMap
    {
        public override TOut Match<TOut>(Func<TOut> none, Func<SavedState, TOut> saved, Func<string, TOut> unreadable) => saved(State);
    }

    internal sealed record Unreadable(string Reason) : PreviousMap
    {
        public override TOut Match<TOut>(Func<TOut> none, Func<SavedState, TOut> saved, Func<string, TOut> unreadable) => unreadable(Reason);
    }
}

/// <summary>The written map, what was found at its path before, and where an unreadable file there was copied to.</summary>
internal sealed record MapWritten(string Path, PreviousMap Previous, Option<string> Backup);

/// <summary>
/// Writes the AUTOAMKB map: one HTML file that opens in a browser without AutoCAD. It shows the drawing around the
/// alignments with every finding under the same ID as in the Excel file, and the car-passage bands of the
/// narrow-road check; clicking a LER line shows its LER data, and clicking a block its attributes. In the map, Montagehul B can be changed (globally or
/// per alignment) and the narrow roads are redone from the measurements stored here; findings can be marked as not
/// a problem; the map saves its own state and writes the final Excel file.
/// </summary>
internal static class AutoAmkBMap
{
    private const string TemplateResource = "IntersectUtilities.MPE.AutoAmkB.AutoAmkBKort.html";
    private const double OriginMargin = 100.0;

    /// <summary>
    /// Writes the map, carrying over what was saved in the map already at that path (Montagehul B, alignment
    /// overrides and not-a-problem marks), so a rerun does not throw away the review. A file there that cannot be
    /// read is copied aside before it is replaced.
    /// </summary>
    public static Result<MapWritten> Write(AmkReport report, Database drawing, string outputPath)
    {
        PreviousMap previous = PreviousMap.Read(outputPath);
        return previous.Match(
                () => Result<Option<string>>.Success(Option<string>.Nothing),
                _ => Result<Option<string>>.Success(Option<string>.Nothing),
                _ => Backup(outputPath).Map(Option<string>.Of))
            .Bind(backup => WriteMap(report, drawing, outputPath, previous.Match(() => "{}", state => state.Json, _ => "{}"))
                .Map(path => new MapWritten(path, previous, backup)));
    }

    private static Result<string> Backup(string path)
    {
        string backup = Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + "_backup.html");
        return Boundary.Try(
            () =>
            {
                File.Copy(path, backup, true);
                return backup;
            },
            $"Det eksisterende kort kunne hverken læses eller kopieres, så det er ikke overskrevet: {path}");
    }

    private static Result<string> WriteMap(AmkReport report, Database drawing, string outputPath, string stateJson)
    {
        List<Point2d> path = report.Traces.SelectMany(trace => trace.Path).Select(sample => sample.Point).ToList();
        // Coordinates are written relative to an origin near the project, which keeps the numbers short.
        Point2d origin = path.Count == 0
            ? Point2d.Origin
            : new Point2d(Math.Floor(path.Min(point => point.X) - OriginMargin), Math.Floor(path.Min(point => point.Y) - OriginMargin));

        return Template().Bind(template =>
            MapDrawing.Read(drawing, new MapCorridor(path)).Bind(background =>
                Boundary.Try(
                        () => new[]
                        {
                            ("__DATA__", DataJson(report, background, origin)),
                            ("__PASSAGE__", PassageJson(report, origin)),
                            ("__STATE__", stateJson),
                        },
                        "Kortets data kunne ikke skrives som JSON")
                    .Bind(parts => Fill(template, parts))
                    .Bind(html => Boundary.Try(
                        () =>
                        {
                            File.WriteAllText(outputPath, html, new UTF8Encoding(false));
                            return outputPath;
                        },
                        $"Kortet kunne ikke skrives. Er det åbent et andet sted? {outputPath}"))));
    }

    // The default encoder escapes <, > and & as well as non-ASCII, so the JSON is safe inside a <script> element.
    // LER data goes in as one string table and one list of distinct line descriptions; a line refers to its
    // description by index (-1 for none). Values repeat a lot, so this keeps it to a fraction of plain text.
    // A block's name and fields are in the same string table; it lists its box and the layers it is drawn on by
    // their index, so the page can leave it alone when all of them are switched off, and its lines as layer index,
    // first line and line count, for highlighting them.
    private static string DataJson(AmkReport report, MapBackground background, Point2d origin)
    {
        Dictionary<string, AlignmentTrace> traces = report.Traces
            .GroupBy(trace => trace.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        double X(Point2d point) => Math.Round(point.X - origin.X, 2);
        double Y(Point2d point) => Math.Round(point.Y - origin.Y, 2);
        double[] Flat(IEnumerable<Point2d> points) => points.SelectMany(point => new[] { X(point), Y(point) }).ToArray();

        StringTable strings = new StringTable();
        int[][] lerInfos = background.LerInfos
            .Select(info => new[] { strings.Of(info.Kind) }
                .Concat(info.Fields.SelectMany(field => new[] { strings.Of(field.Label), strings.Of(field.Value) }))
                .ToArray())
            .ToArray();

        IReadOnlyDictionary<string, int> layerIndex = background.Layers
            .Select((layer, index) => (layer.Name, index))
            .ToDictionary(layer => layer.Name, layer => layer.index, StringComparer.OrdinalIgnoreCase);
        // Listed here and not while the JSON is written: by then the string table has already gone out.
        var blocks = background.Blocks
            .Select(block => new
            {
                n = strings.Of(block.Name),
                b = new[] { X(block.Min), Y(block.Min), X(block.Max), Y(block.Max) },
                l = block.Layers.Select(name => layerIndex.Lookup(name)).Values().ToArray(),
                r = block.Lines
                    .SelectMany(lines => layerIndex.Lookup(lines.Layer)
                        .Map(index => new[] { index, lines.First, lines.Count })
                        .OrElse(Array.Empty<int>()))
                    .ToArray(),
                f = block.Fields.SelectMany(field => new[] { strings.Of(field.Label), strings.Of(field.Value) }).ToArray(),
            })
            .ToArray();

        // runId comes first: the page finds it in a saved copy of itself to tell whether that copy is this run.
        return JsonSerializer.Serialize(new
        {
            runId = Guid.NewGuid().ToString("N"),
            project = report.ProjectId,
            etape = report.EtapeId,
            drawing = Path.GetFileName(report.DrawingPath),
            runTime = report.RunTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            origin = new[] { origin.X, origin.Y },
            excel = ExcelTexts(report),
            lerStrings = strings.Texts,
            lerInfos,
            blocks,
            layers =background.Layers.Select(layer => new
            {
                name = layer.Name,
                rgb = layer.Colour,
                lines = layer.Lines.Select(line => Flat(line.Points)),
                info = layer.Lines.Any(line => line.Info.IsSome())
                    ? layer.Lines.Select(line => line.Info.OrElse(-1)).ToArray()
                    : Array.Empty<int>(),
                texts = layer.Texts.Select(text => new object[]
                {
                    X(text.Position), Y(text.Position), Math.Round(text.Height, 3), Math.Round(text.Rotation, 4), text.Text,
                }),
            }),
            alignments = report.Traces.Select(trace => new
            {
                name = trace.Name,
                pts = Flat(trace.Path.Select(sample => sample.Point)),
                st = trace.Path.Select(sample => Math.Round(sample.Station, 3)),
                ticks = trace.Ticks.SelectMany(tick => new[]
                {
                    Math.Round(tick.Station, 2), X(tick.Point), Y(tick.Point), Math.Round(tick.Right.X, 4), Math.Round(tick.Right.Y, 4),
                }),
            }),
            hits = report.Hits.Select(hit => new
            {
                kind = hit.Kind.Key,
                kindName = hit.Kind.Name,
                id = StationText.Of(hit.Alignment, hit.Extent),
                stretch = hit.Extent.Match(_ => false, (_, _) => true),
                alignment = hit.Alignment,
                from = Math.Round(hit.Extent.From, 2),
                to = Math.Round(hit.Extent.To, 2),
                detail = hit.Trace.Detail,
                owner = hit.Trace.Owner,
                source = hit.Trace.Source,
                values = hit.Values,
                count = hit.Trace.Count,
                handle = hit.Trace.Handle,
                tx = Math.Round(hit.Trace.X, 3),
                ty = Math.Round(hit.Trace.Y, 3),
                pts = Flat(HitPoints(hit, traces)),
            }),
            valves = report.Valves.Select(location => new
            {
                id = location.Alignment.Bind(name => location.Station.Map(station => StationText.Point(name, station))).OrElse(""),
                alignment = location.Alignment.OrElse(""),
                station = location.Station.Map(station => AmkNumbers.Text(station, "0.0")).OrElse(""),
                sta = location.Station.Map(station => new[] { Math.Round(station, 2) }).OrElse(Array.Empty<double>()),
                desc = AmkTemplate.JoinDistinct(location.Valves.Select(valve => $"{valve.Designation} DN{valve.Dn} {valve.System}"), " / "),
                values = new Dictionary<string, string>
                {
                    ["betegnelse"] = AmkTemplate.JoinDistinct(location.Valves.Select(valve => valve.Designation), " / "),
                    ["dn"] = AmkTemplate.JoinDistinct(location.Valves.Select(valve => valve.Dn), "/"),
                    ["system"] = AmkTemplate.JoinDistinct(location.Valves.Select(valve => valve.System), "/"),
                },
                count = location.Valves.Count,
                types = AmkTemplate.JoinDistinct(location.Valves.Select(valve => valve.ElementType), ", "),
                blocks = AmkTemplate.JoinDistinct(location.Valves.Select(valve => valve.BlockName), ", "),
                handles = string.Join(", ", location.Valves.Select(valve => valve.Handle)),
                fx = Math.Round(location.Valves[0].X, 3),
                fy = Math.Round(location.Valves[0].Y, 3),
                x = Math.Round(location.Valves.Average(valve => valve.X) - origin.X, 2),
                y = Math.Round(location.Valves.Average(valve => valve.Y) - origin.Y, 2),
            }),
        });
    }

    // What the page needs to write the Excel file itself: the texts of every kind and of the journal from
    // AutoAmkB.csv, and the run information for the Kørselsinfo sheet.
    private static object ExcelTexts(AmkReport report) => new
    {
        kinds = HitKind.All.Select(kind =>
        {
            HitTexts texts = report.Rules.Rules.TextsFor(kind);
            return new
            {
                key = kind.Key,
                name = kind.Name,
                topic = texts.Topic,
                description = texts.Description,
                main = texts.MainCategory,
                sub = texts.SubCategory,
                criterionTopic = texts.CriterionTopic,
                criterionDescription = texts.CriterionDescription,
            };
        }),
        journal = new
        {
            main = report.Rules.Rules.Journal.MainCategory,
            subs = report.Rules.Rules.Journal.SubCategories,
            description = report.Rules.Rules.Journal.Description,
        },
        info = new
        {
            drawingPath = report.DrawingPath,
            user = Environment.UserName,
            configuration = report.Configuration,
            rulesSource = report.Rules.Source,
            notEvaluated = report.NotEvaluated,
            files = report.DataFiles.Select(file => new[]
            {
                file.Role,
                file.Path,
                file.Modified.Match(time => time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), () => ""),
            }),
            warnings = report.Warnings.Distinct(),
            rules = report.Rules.Rules.Entries.Select(entry => new[] { entry.Key, entry.Value, entry.Note }),
        },
    };

    // d, for the bands (every 0.5 m where the trench width is known): station, x, y, right-hand unit vector, trench
    // width, then the left and right road-edge gaps. g, for redoing the check (every check sample): the left and
    // right gaps in metres, -1 for no road edge and -2 where the trench width is unknown, as the exact doubles in
    // base64 (little-endian), so the page flags exactly the samples AUTOAMKB flags; rounded to mm, samples within half
    // a millimetre of the limit flipped. Sample i sits at s0 + i * step, the last one at last. w: the trench width in
    // metres from each sample index where it changes.
    private static string PassageJson(AmkReport report, Point2d origin)
    {
        AmkRules rules = report.Rules.Rules;
        return JsonSerializer.Serialize(new
        {
            B = rules.RoadBarrierAllowance,
            need = rules.RoadMinFreeWidth,
            search = rules.RoadSearchDistance,
            step = AlignmentTraces.PassageStep,
            sampleStep = rules.SampleStep,
            joinGap = rules.StretchJoinGap,
            minLength = rules.RoadMinStretchLength,
            alignments = report.Traces.Where(trace => trace.Sections.Count > 0).Select(trace => new
            {
                name = trace.Name,
                d = trace.Passage.SelectMany(section => section.TrenchWidth.Match(
                    width => new[]
                    {
                        Math.Round(section.Sample.Station, 2),
                        Math.Round(section.Sample.Point.X - origin.X, 2),
                        Math.Round(section.Sample.Point.Y - origin.Y, 2),
                        Math.Round(section.Sample.Right.X, 4),
                        Math.Round(section.Sample.Right.Y, 4),
                        Math.Round(width, 3),
                        Gap(section.GapLeft),
                        Gap(section.GapRight),
                    },
                    Array.Empty<double>)),
                s0 = trace.Sections[0].Sample.Station,
                last = trace.Sections[^1].Sample.Station,
                n = trace.Sections.Count,
                g = ExactGaps(trace.Sections),
                w = WidthRuns(trace.Sections),
            }),
        });
    }

    // The page reads -1 as "no road edge within the search distance".
    private static double Gap(Option<double> gap) => gap.Map(value => Math.Round(value, 2)).OrElse(-1);

    private static string ExactGaps(IReadOnlyList<SampleSection> sections)
    {
        double[] gaps = sections
            .SelectMany(section => section.TrenchWidth.Match(
                _ => new[] { section.GapLeft.OrElse(-1), section.GapRight.OrElse(-1) },
                () => new[] { -2.0, -2.0 }))
            .ToArray();
        byte[] bytes = new byte[gaps.Length * sizeof(double)];
        Buffer.BlockCopy(gaps, 0, bytes, 0, bytes.Length);
        return Convert.ToBase64String(bytes);
    }

    private static IReadOnlyList<double> WidthRuns(IReadOnlyList<SampleSection> sections)
    {
        List<double> runs = new List<double>();
        double current = -1;
        for (int i = 0; i < sections.Count; i++)
        {
            double width = sections[i].TrenchWidth.OrElse(current);
            if (width == current) continue;
            runs.Add(i);
            runs.Add(width);
            current = width;
        }
        return runs;
    }

    // A stretch follows its alignment and a point sits on it; without the alignment the hit's own position is used.
    private static IReadOnlyList<Point2d> HitPoints(Hit hit, IReadOnlyDictionary<string, AlignmentTrace> traces) =>
        traces.Lookup(hit.Alignment)
            .Map(trace => hit.Extent.Match(
                station => trace.At(station).Match(point => (IReadOnlyList<Point2d>)new[] { point }, Array.Empty<Point2d>),
                (from, to) => trace.Between(from, to)))
            .Where(points => points.Count > 0)
            .OrElse(new[] { new Point2d(hit.Trace.X, hit.Trace.Y) });

    private sealed class StringTable
    {
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>(StringComparer.Ordinal);

        public List<string> Texts { get; } = new List<string>();

        public int Of(string text)
        {
            if (!_index.TryGetValue(text, out int index))
            {
                index = Texts.Count;
                Texts.Add(text);
                _index[text] = index;
            }
            return index;
        }
    }

    private static Result<string> Template() =>
        Boundary.Try(
                () => AmkNullable.Of(typeof(AutoAmkBMap).Assembly.GetManifestResourceStream(TemplateResource)).Map(ReadAll),
                "Kortskabelonen kunne ikke læses")
            .Bind(template => template.Match(
                Result<string>.Success,
                () => Result<string>.Failure($"Kortskabelonen {TemplateResource} mangler i IntersectUtilities.dll.")));

    private static string ReadAll(Stream stream)
    {
        using (stream)
        {
            using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }

    // Each marker is looked up in the template only, never in data already put in, so drawing texts cannot clash.
    private static Result<string> Fill(string template, IReadOnlyList<(string Marker, string Value)> parts)
    {
        StringBuilder html = new StringBuilder(template.Length + parts.Sum(part => part.Value.Length));
        int position = 0;
        foreach ((string marker, string value) in parts)
        {
            int at = template.IndexOf(marker, position, StringComparison.Ordinal);
            if (at < 0) return Result<string>.Failure($"Kortskabelonen mangler {marker}.");
            html.Append(template, position, at - position).Append(value);
            position = at + marker.Length;
        }
        html.Append(template, position, template.Length - position);
        return Result<string>.Success(html.ToString());
    }
}
