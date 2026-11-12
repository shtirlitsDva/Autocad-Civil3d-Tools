using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using IntersectUtilities.LongitudinalProfiles;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Unit = IntersectUtilities.LongitudinalProfiles.Unit;

namespace IntersectUtilities.LongitudinalProfiles.Quality;

internal static partial class ProfileQaCommands
{
    private const string ClearanceLayer = "0-PROFILE_AFSTANDS_MARKERING";
    private sealed record QaMarker(string Key, double Size, QaPoint Centre, QaRegion Region, string Handle);
    private sealed record QaSymbol(string Layer, QaPoint Centre, double AnchorStation, double TopElevation);
    private sealed record QaCogo(double Station, string Layer, uint Number, string Name, string Description, string Source, string Relocatable, bool Unknown, string Diameter);
    internal sealed record QaCrossing(string Group, double Station, string Identity, string Depth, string Relocatable, string Source,
        IReadOnlyList<QaInterval> Conflicts, string Diameter, string Material, Option<double> CasingTop);

    private static Unit ReportCrossings(Editor editor, Transaction transaction, QaInput input)
    {
        return SelectCrossingBlock(editor, transaction, input).Bind(block => CollectCrossings(transaction, input, block)).Match(crossings =>
        {
            int known = crossings.Count(c => c.Depth == "known");
            int conflicts = crossings.Count(c => c.Depth == "known" && c.Conflicts.Count > 0);
            int unknownReviews = crossings.Count(c => c.Depth == "UNKNOWN");
            editor.WriteMessage("\nKnown-depth conflicts plus unknown-depth crossings for review. ? = missing data.");
            editor.WriteMessage($"\nTouching allowed; crossing contact tolerance {QaGeometry.CrossingContactTolerance * 1000:0.########} mm.");
            editor.WriteMessage("\nUAD / ude af drift crossings excluded for both known and unknown depths.");
            editor.WriteMessage("\nUnknown depths only: tele, KOM, 0.4 kV and vejbelysning excluded; all other types included.");
            editor.WriteMessage("\nUNKNOWN rows require depth confirmation; their Intersects value uses the drawn placeholder position.");
            var sizes = ReadCrossingPipes(input).Match(pipes => pipes.Sizes,
                message => { editor.WriteMessage($"\nDistrict-heating data unavailable: {message} DH pipe shown as ?."); return (IReadOnlyList<QaPipeSpan>)Array.Empty<QaPipeSpan>(); });
            var surface = ReadTerrain(transaction, input).Match(terrain => terrain.Segments,
                message => { editor.WriteMessage($"\nTop depths unavailable: {message}"); return (IReadOnlyList<QaSegment>)Array.Empty<QaSegment>(); });
            var details = AddCrossingDetails(editor, crossings.Where(c => c.Depth == "UNKNOWN" || c.Conflicts.Count > 0).ToArray());
            WriteCrossingRows(editor, input, sizes, surface, details);
            editor.WriteMessage($"\n\n{known} known-depth crossings checked; {conflicts} confirmed clearance conflicts; {unknownReviews} unknown-depth crossings for review.");
            return Unit.Value;
        }, message => { editor.WriteMessage($"\nCOULD NOT CHECK crossings: {message}"); return Unit.Value; });
    }

    internal static Result<ObjectId> FindCrossingBlock(Transaction transaction, QaInput input)
    {
        var space = (BlockTableRecord)transaction.GetObject(input.View.OwnerId, OpenMode.ForRead);
        var blocks = space.Cast<ObjectId>().Select(id => transaction.GetObject(id, OpenMode.ForRead)).OfType<BlockReference>()
            .Select(block => (block.ObjectId, ((BlockTableRecord)transaction.GetObject(block.BlockTableRecord, OpenMode.ForRead)).Name));
        return QaCrossingBlockSelection.Find(blocks, input.View.Name);
    }

    private static Result<BlockReference> SelectCrossingBlock(Editor editor, Transaction transaction, QaInput input) =>
        FindCrossingBlock(transaction, input).Match(id =>
        {
            var block = (BlockReference)transaction.GetObject(id, OpenMode.ForRead);
            editor.WriteMessage($"\nAutomatically selected crossing/detailing block: {input.View.Name} (handle {block.Handle}).");
            return Result<BlockReference>.Success(block);
        }, message =>
        {
            editor.WriteMessage($"\n{message} Select the block manually.");
            var options = new PromptEntityOptions("\nSelect this view's crossing/detailing block: ");
            options.SetRejectMessage("\nSelect a block reference."); options.AddAllowedClass(typeof(BlockReference), true);
            var pick = editor.GetEntity(options);
            if (pick.Status != PromptStatus.OK) return Result<BlockReference>.Failure("Cancelled.");
            var block = (BlockReference)transaction.GetObject(pick.ObjectId, OpenMode.ForRead);
            string name = ((BlockTableRecord)transaction.GetObject(block.BlockTableRecord, OpenMode.ForRead)).Name;
            if (!string.Equals(name, input.View.Name, StringComparison.OrdinalIgnoreCase) || block.OwnerId != input.View.OwnerId)
                return Result<BlockReference>.Failure($"Block '{name}' does not match view '{input.View.Name}' in its drawing space.");
            return Result<BlockReference>.Success(block);
        });

    internal static void WriteCrossingRows(Editor editor, QaInput input, IReadOnlyList<QaPipeSpan> sizes,
        IReadOnlyList<QaSegment> surface, IReadOnlyList<QaCrossing> crossings)
    {
        editor.WriteMessage("\nUtility top / DH top: vertical depth below the surface at the crossing station, in metres.");
        editor.WriteMessage("\nUtility top follows the drawn utility casing. UNKNOWN = unverified utility depth; ? = unavailable.");
        var tables = new List<(string Group, QaTable Table)>();
        foreach (var group in crossings.GroupBy(c => c.Group).OrderBy(g => g.Key))
        {
            var table = new QaTable(new("Crossing"), new("Station", true), new("Depth known"), new("Intersects"), new("Diameter"),
                new("Material", MaxWidth: 18), new("Utility top", true), new("DH top", true), new("DH pipe", MaxWidth: int.MaxValue));
            foreach (var crossing in group.OrderBy(c => c.Station))
            {
                var terrain = QaLevels.ElevationAt(surface, crossing.Station);
                string utilityDepth = crossing.Depth == "UNKNOWN" ? "UNKNOWN" : DepthText(QaLevels.Depth(terrain, crossing.CasingTop));
                string dhDepth = DepthText(QaLevels.Depth(terrain, QaLevels.CasingTopAt(input.Segments, sizes, crossing.Station)));
                table.Add(crossing.Identity, crossing.Station.ToString("F3"), crossing.Depth == "known" ? "Yes" : "No", crossing.Conflicts.Count > 0 ? "True" : "False",
                    crossing.Diameter, crossing.Material, utilityDepth, dhDepth, DistrictHeatingAt(sizes, crossing.Station));
            }
            tables.Add((group.Key, table));
        }
        int[] widths = QaTable.SharedWidths(tables.Select(t => t.Table));
        foreach (var table in tables)
        {
            editor.WriteMessage($"\n\n{table.Group}");
            WriteTable(editor, table.Table, widths);
        }
    }

    private static string DepthText(Option<double> value) => value.Match(depth => depth.ToString("F2"), () => "?");

    internal static Result<IReadOnlyList<QaCrossing>> CollectCrossings(Transaction transaction, QaInput input, BlockReference block)
    {
        var markers = new List<QaMarker>(); var symbols = new List<QaSymbol>(); var problems = new List<string>();
        CollectBlock(transaction, input.Map, block.BlockTableRecord, block.BlockTransform, "0", markers, symbols, problems, 0);
        if (problems.Count > 0) return Result<IReadOnlyList<QaCrossing>>.Failure(string.Join("; ", problems.Distinct()));
        if (markers.Count == 0) return Result<IReadOnlyList<QaCrossing>>.Failure($"No envelope curves found on {ClearanceLayer}.");
        var points = new List<QaCogo>();
        var space = (BlockTableRecord)transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(input.View.Database), OpenMode.ForRead);
        foreach (ObjectId id in space)
        {
            if (transaction.GetObject(id, OpenMode.ForRead) is not CogoPoint point || Text(point, "DriCrossingData", "Alignment") != input.Alignment.Name) continue;
            double station = Station(input.Alignment, point.Location);
            string description = point.RawDescription;
            bool unknown = description.Contains("ukendt", StringComparison.OrdinalIgnoreCase) || description.Contains("unknown", StringComparison.OrdinalIgnoreCase) || point.Elevation <= -98.99;
            points.Add(new(station, point.Layer, point.PointNumber, point.PointName, description,
                Text(point, "DriCrossingData", "SourceEntityHandle"), Text(point, "DriCrossingData", "CanBeRelocated"), unknown,
                QaCrossingDetailsReader.DiameterText(Text(point, "DriCrossingData", "Diameter"), "mm")));
        }
        var results = new List<QaCrossing>();
        foreach (var group in markers.GroupBy(m => m.Key))
        {
            var ordered = group.OrderByDescending(m => m.Size).ToArray();
            QaMarker outer = ordered[0];
            double envelopeStart = outer.Region.Edges.Min(e => e.Kind == QaEdgeKind.Line ? Math.Min(e.P.S, e.Q.S) : e.Cx - e.Rx);
            double envelopeEnd = outer.Region.Edges.Max(e => e.Kind == QaEdgeKind.Line ? Math.Max(e.P.S, e.Q.S) : e.Cx + e.Rx);
            if (envelopeEnd < input.Start || envelopeStart > input.End) continue;
            var nearestSymbols = symbols.Select(s => (Symbol: s, Distance: Math.Sqrt(Math.Pow(s.Centre.S - outer.Centre.S, 2) + Math.Pow(s.Centre.Z - outer.Centre.Z, 2))))
                .OrderBy(s => s.Distance).ToArray();
            var candidates = nearestSymbols.Where(s => s.Distance < 1e-4).Select(s => s.Symbol).ToArray();
            string layer = candidates.Length == 1 ? candidates[0].Layer : "UNCLASSIFIED";
            double crossingStation = candidates.Length == 1 ? candidates[0].AnchorStation : outer.Centre.S;
            var cogoCandidates = points.Where(p => Math.Abs(p.Station - crossingStation) < 1e-4 && (layer == "UNCLASSIFIED" || string.Equals(p.Layer, layer, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (cogoCandidates.Length != 1) continue;
            var matchedPoint = cogoCandidates[0];
            if (!QaCrossingPolicy.IncludeCrossing(matchedPoint.Unknown, matchedPoint.Layer, matchedPoint.Description)) continue;
            if (ordered.Length < 2)
                return Result<IReadOnlyList<QaCrossing>>.Failure($"Envelope {outer.Handle} has no identifiable inner/outer pair; outer clearance cannot be verified.");
            string depth = "unlinked", identity = $"envelope {outer.Handle}", source = "--", relocatable = "--", category = layer, diameter = "?";
            if (cogoCandidates.Length == 1)
            {
                var point = cogoCandidates[0];
                depth = point.Unknown ? "UNKNOWN" : "known";
                identity = $"COGO {point.Number}"; source = point.Source; relocatable = point.Relocatable; diameter = point.Diameter;
                category = layer == "UNCLASSIFIED" ? point.Layer : layer;
                if (point.Description.Contains("Spildevand", StringComparison.OrdinalIgnoreCase)) category += " / Spildevand";
                else if (point.Description.Contains("Regnvand", StringComparison.OrdinalIgnoreCase)) category += " / Regnvand";
            }
            var intrusions = new List<QaInterval>();
            foreach (var curve in input.Segments)
            {
                double start = Math.Max(input.Start, curve.Start), end = Math.Min(input.End, curve.End);
                if (end > start) intrusions.AddRange(QaGeometry.Intrusions(curve, outer.Region, start, end));
            }
            var casingTop = !matchedPoint.Unknown && candidates.Length == 1 ? Option<double>.Of(candidates[0].TopElevation) : Option<double>.Nothing;
            results.Add(new(category, crossingStation, identity, depth, relocatable, source, QaGeometry.Merge(intrusions), diameter, "?", casingTop));
        }
        // Above/below semicircular envelopes can belong to the same COGO point. Present one crossing row.
        return Result<IReadOnlyList<QaCrossing>>.Success(results.GroupBy(c => (c.Group, c.Identity, c.Station)).Select(g =>
        {
            var first = g.First();
            return first with { Conflicts = QaGeometry.Merge(g.SelectMany(c => c.Conflicts)) };
        }).ToArray());
    }

    internal static string DistrictHeatingAt(IReadOnlyList<QaPipeSpan> sizes, double station)
    {
        var labels = sizes.Where(s => station >= s.Start - QaGeometry.StationTolerance && station <= s.End + QaGeometry.StationTolerance)
            .Select(QaPipeText.Description).Distinct().ToArray();
        return labels.Length == 0 ? "?" : string.Join(" / ", labels);
    }

    internal static IReadOnlyList<QaCrossing> AddCrossingDetails(Editor editor, IReadOnlyList<QaCrossing> crossings)
    {
        if (crossings.Count == 0) return crossings;
        return QaCrossingDetailsReader.Create().Match(reader =>
        {
            using (reader)
            {
                var enriched = new List<QaCrossing>();
                int unavailable = 0;
                foreach (var crossing in crossings)
                    reader.Read(crossing.Source).Match(details =>
                    {
                        enriched.Add(crossing with { Diameter = details.Diameter == "?" ? crossing.Diameter : details.Diameter, Material = details.Material });
                        return Unit.Value;
                    }, message => { unavailable++; enriched.Add(crossing); return Unit.Value; });
                if (unavailable > 0) editor.WriteMessage($"\nLER details unavailable for {unavailable} crossings; missing values shown as ?.");
                return (IReadOnlyList<QaCrossing>)enriched;
            }
        }, message => { editor.WriteMessage($"\nLER details unavailable: {message} Missing values shown as ?."); return crossings; });
    }

    private static void CollectBlock(Transaction transaction, QaViewMap map, ObjectId definition, Matrix3d transform, string inheritedLayer,
        List<QaMarker> markers, List<QaSymbol> symbols, List<string> problems, int depth)
    {
        if (depth > 8) { problems.Add("Detailing block nesting exceeds eight levels."); return; }
        var record = (BlockTableRecord)transaction.GetObject(definition, OpenMode.ForRead);
        foreach (ObjectId id in record)
        {
            if (transaction.GetObject(id, OpenMode.ForRead) is not Entity entity) continue;
            string layer = entity.Layer == "0" ? inheritedLayer : entity.Layer;
            if (layer == ClearanceLayer)
            {
                ReadMarker(entity, transform, map).Match(marker => { markers.Add(marker); return Unit.Value; }, message => { problems.Add(message); return Unit.Value; });
                continue;
            }
            if (entity is BlockReference nested)
            {
                var child = (BlockTableRecord)transaction.GetObject(nested.BlockTableRecord, OpenMode.ForRead);
                bool hasEnvelopes = child.Cast<ObjectId>().Select(i => transaction.GetObject(i, OpenMode.ForRead)).OfType<Entity>().Any(e => e.Layer == ClearanceLayer);
                if (hasEnvelopes)
                    CollectBlock(transaction, map, nested.BlockTableRecord, transform * nested.BlockTransform, layer, markers, symbols, problems, depth + 1);
                else
                {
                    Extents3d box = nested.GeometricExtents;
                    Point3d centre = new((box.MinPoint.X + box.MaxPoint.X) / 2, (box.MinPoint.Y + box.MaxPoint.Y) / 2, 0);
                    symbols.Add(new(layer, map.FromWorld(centre.TransformBy(transform)), map.FromWorld(nested.Position.TransformBy(transform)).S,
                        SymbolTop(box, transform, map)));
                }
            }
            else if (entity is Circle circle)
            {
                QaPoint centre = map.FromWorld(circle.Center.TransformBy(transform));
                symbols.Add(new(layer, centre, centre.S, SymbolTop(circle.GeometricExtents, transform, map)));
            }
            else if (entity is Polyline polyline && polyline.Closed)
            {
                Extents3d box = polyline.GeometricExtents;
                QaPoint centre = map.FromWorld(new Point3d((box.MinPoint.X + box.MaxPoint.X) / 2, (box.MinPoint.Y + box.MaxPoint.Y) / 2, 0).TransformBy(transform));
                symbols.Add(new(layer, centre, centre.S, SymbolTop(box, transform, map)));
            }
        }
    }

    private static double SymbolTop(Extents3d box, Matrix3d transform, QaViewMap map) => new[]
    {
        box.MinPoint, box.MaxPoint,
        new Point3d(box.MinPoint.X, box.MaxPoint.Y, box.MinPoint.Z),
        new Point3d(box.MaxPoint.X, box.MinPoint.Y, box.MinPoint.Z)
    }.Max(p => map.FromWorld(p.TransformBy(transform)).Z);

    private static Result<QaMarker> ReadMarker(Entity entity, Matrix3d transform, QaViewMap map)
    {
        string handle = entity.Handle.ToString();
        if (entity is Circle circle)
            return MappedArc(circle.Center, circle.Radius, 0, 2 * Math.PI, transform, map).Map(edge =>
                new QaMarker(Key("C", new(edge.Cx, edge.Cz)), edge.Rx * edge.Ry, new(edge.Cx, edge.Cz), new(new[] { edge }), handle));
        if (entity is Arc arc)
        {
            double sweep = QaGeometry.PositiveAngle(arc.EndAngle - arc.StartAngle);
            if (Math.Abs(sweep - Math.PI) > 1e-6)
                return Result<QaMarker>.Failure($"Envelope arc {handle} is not a semicircle; its forbidden side is ambiguous.");
            return MappedArc(arc.Center, arc.Radius, arc.StartAngle, sweep, transform, map).Map(edge =>
                new QaMarker(Key("A" + Math.Round(QaGeometry.PositiveAngle(edge.Angle), 6).ToString(CultureInfo.InvariantCulture), new(edge.Cx, edge.Cz)),
                    edge.Rx * edge.Ry, new(edge.Cx, edge.Cz), new(new[] { edge, QaEdge.Line(edge.Q, edge.P) }), handle));
        }
        if (entity is Polyline polyline && polyline.Closed)
        {
            var edges = new List<QaEdge>();
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                int next = (i + 1) % polyline.NumberOfVertices;
                if (polyline.GetBulgeAt(i) == 0)
                    edges.Add(QaEdge.Line(map.FromWorld(polyline.GetPoint3dAt(i).TransformBy(transform)), map.FromWorld(polyline.GetPoint3dAt(next).TransformBy(transform))));
                else
                {
                    var circular = polyline.GetArcSegment2dAt(i);
                    Point3d p = polyline.GetPoint3dAt(i);
                    double angle = Math.Atan2(p.Y - circular.Center.Y, p.X - circular.Center.X);
                    var result = MappedArc(new(circular.Center.X, circular.Center.Y, polyline.Elevation), circular.Radius, angle, 4 * Math.Atan(polyline.GetBulgeAt(i)), transform, map);
                    string issue = result.Match(edge => { edges.Add(edge); return ""; }, message => message);
                    if (issue.Length > 0) return Result<QaMarker>.Failure(issue);
                }
            }
            Extents3d box = polyline.GeometricExtents;
            QaPoint centre = map.FromWorld(new Point3d((box.MinPoint.X + box.MaxPoint.X) / 2, (box.MinPoint.Y + box.MaxPoint.Y) / 2, 0).TransformBy(transform));
            return Result<QaMarker>.Success(new(Key("P", centre), (box.MaxPoint.X - box.MinPoint.X) * (box.MaxPoint.Y - box.MinPoint.Y), centre, new(edges), handle));
        }
        return Result<QaMarker>.Failure($"Unsupported envelope entity {entity.GetType().Name}, handle {handle}.");
    }

    private static string Key(string kind, QaPoint centre) => $"{kind}:{Math.Round(centre.S, 5).ToString(CultureInfo.InvariantCulture)}:{Math.Round(centre.Z, 5).ToString(CultureInfo.InvariantCulture)}";

    private static Result<QaEdge> MappedArc(Point3d centre, double radius, double angle, double sweep, Matrix3d transform, QaViewMap map)
    {
        QaPoint c = map.FromWorld(centre.TransformBy(transform));
        QaPoint x = map.FromWorld(new Point3d(centre.X + radius, centre.Y, centre.Z).TransformBy(transform));
        QaPoint y = map.FromWorld(new Point3d(centre.X, centre.Y + radius, centre.Z).TransformBy(transform));
        if (Math.Abs(x.Z - c.Z) > 1e-7 || Math.Abs(y.S - c.S) > 1e-7 || Math.Abs(x.S - c.S) < 1e-10 || Math.Abs(y.Z - c.Z) < 1e-10)
            return Result<QaEdge>.Failure("Rotated or degenerate crossing-envelope transforms are unsupported by this draft.");
        double rx = Math.Abs(x.S - c.S), ry = Math.Abs(y.Z - c.Z);
        double mappedAngle = Math.Atan2(Math.Sign(y.Z - c.Z) * Math.Sin(angle), Math.Sign(x.S - c.S) * Math.Cos(angle));
        double mappedSweep = sweep * Math.Sign((x.S - c.S) * (y.Z - c.Z));
        return Result<QaEdge>.Success(QaEdge.Arc(c.S, c.Z, rx, ry, mappedAngle, mappedSweep));
    }
}
