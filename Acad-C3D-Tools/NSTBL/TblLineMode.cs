using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using CSharpFunctionalExtensions;

using IntersectUtilities.Tbl;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;
using IntersectUtilities.UtilsCommon.Enums;

using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Buffer;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.UtilsCommon.Utils;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Result = CSharpFunctionalExtensions.Result;

namespace IntersectUtilities.NSTBL
{
    /// <summary>Why the line-mode export cannot run: NSTBL's one error contract.</summary>
    internal sealed record TblError(string Code, string Message)
    {
        /// <summary>The drawing does not hold what the export needs (the xref, its file).</summary>
        public const string DrawingCode = "TBL_DRAWING";

        /// <summary>The CAD program or the file system threw.</summary>
        public const string HostCode = "TBL_HOST";

        public static TblError Drawing(string message) => new(DrawingCode, message);

        public static TblError Host(string what, Exception ex) => new(HostCode, $"{what}: {ex.GetType().Name}: {ex.Message}");

        public override string ToString() => Message;
    }

    /// <summary>
    /// A component as NSALIGNMENTCRAWL routes through it (NSAlignmentCrawlNetworkReader / GraphBuilder in
    /// IntersectUtilities): its MuffeIntern ports and, when it has them, its centreline chains, the Line, Arc and
    /// Polyline entities on a layer whose name holds "komponent". A component with ports and chains is crawled
    /// along its chains, so its insertion point need not lie on the path (an F-model's retur chain joins its
    /// twin chain 0.4 m from the insertion point); without chains it is crawled port - insertion - port.
    /// </summary>
    internal sealed record TblComponent(
        BlockReference Br, Coordinate Insertion, IReadOnlyList<Coordinate> Ports, Geometry Chains, double ChainLength)
    {
        private const double AlongShare = 0.5;

        public bool IsCentreline => Ports.Count > 0 && ChainLength > 0;

        public Envelope Reach
        {
            get
            {
                Envelope e = new Envelope(Insertion);
                foreach (Coordinate p in Ports) e.ExpandToInclude(p);
                if (!Chains.IsEmpty) e.ExpandToInclude(Chains.EnvelopeInternal);
                e.ExpandBy(TblLineMode.Tolerance);
                return e;
            }
        }

        /// <summary>
        /// On a stretch: a spoke component (or a block the crawl does not route through) when its insertion point is
        /// within 25 mm of it; a centreline component when the stretch passes two of its ports, or runs along at
        /// least half of its chains within 1 mm (a stretch that ends inside the component).
        /// </summary>
        public bool IsOn(TblLine line)
        {
            if (!line.Corridor.EnvelopeInternal.Intersects(Reach)) return false;
            if (!IsCentreline) return Near(line.Line, Insertion);
            if (Ports.Count(p => Near(line.Line, p)) >= 2) return true;
            return Chains.Intersection(line.Coincident).Length >= AlongShare * ChainLength;
        }

        /// <summary>A stretch end at the component: at its insertion point, or anywhere on its chains or ports.</summary>
        public bool IsAt(Coordinate end) =>
            !IsCentreline
                ? end.Distance(Insertion) <= TblLineMode.Tolerance
                : Ports.Any(p => end.Distance(p) <= TblLineMode.Tolerance) ||
                  Chains.Distance(TblLineGeometry.Point(end)) <= TblLineMode.Tolerance;

        private static bool Near(LineString line, Coordinate c) =>
            line.Distance(TblLineGeometry.Point(c)) <= TblLineMode.Tolerance;

        public static TblComponent Read(BlockReference br, Transaction tx)
        {
            List<Coordinate> ports = new List<Coordinate>();
            List<LineString> chains = new List<LineString>();
            BlockTableRecord btr = (BlockTableRecord)tx.GetObject(br.BlockTableRecord, OpenMode.ForRead);
            Matrix3d toWorld = br.BlockTransform;
            foreach (Oid id in btr)
            {
                DBObject obj = tx.GetObject(id, OpenMode.ForRead);
                if (obj is BlockReference nested)
                {
                    if (Normalized(nested.Name).StartsWith("muffeintern"))
                    {
                        Point3d p = nested.Position.TransformBy(toWorld);
                        ports.Add(new Coordinate(p.X, p.Y));
                    }
                    continue;
                }
                if (obj is not Curve curve || obj is not (Line or Arc or Polyline)) continue;
                if (!Normalized(curve.Layer).Contains("komponent")) continue;
                chains.Add(TblLineGeometry.Sampled(curve, toWorld));
            }

            Geometry chainGeometry = TblLineGeometry.Collect(chains);
            return new TblComponent(br, new Coordinate(br.Position.X, br.Position.Y), ports, chainGeometry, chainGeometry.Length);
        }

        // As the crawl's reader: the name after any xref prefix, without '-', '_' and spaces, lower case.
        private static string Normalized(string name)
        {
            int bar = name.LastIndexOf('|');
            string local = bar >= 0 ? name.Substring(bar + 1) : name;
            return local.Replace("-", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
        }
    }

    /// <summary>A TBL stretch in the Fremtid's own coordinates, with its tags and its 25 mm corridor.</summary>
    internal sealed record TblLine(
        int Order, string Handle, string Layer, string Vejnavn, string Vejklasse, string Belægning,
        LineString Line, Geometry Corridor, Geometry Coincident, Coordinate Start, Coordinate End)
    {
        /// <summary>
        /// On a Belægning's layer whose Belægning is not the one in DriOmråder (a stretch retagged with STD). The
        /// property is what is counted; the bare 0-TBL-STRÆK and other 0-TBL-STRÆK-* layers name no Belægning.
        /// </summary>
        public bool LayerMismatch =>
            TblBelægninger.OfLayer(Layer) is TblBelægning b && !string.Equals(b.Name, Belægning, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Line mode of the TBL export (decision 7): when the drawing holds stretches on 0-TBL-STRÆK or 0-TBL-STRÆK-*
    /// (drawn by TBLCRAWL in IntersectUtilities on top of the FJV Fremtid xref, one layer per Belægning), they
    /// replace the 0-OMRÅDER-OK polygons. Their DriOmråder Belægning is what is counted, whatever the layer.
    /// - Fremtid is read from the xref the stretches were drawn on, not from Stier.csv; a Stier.csv Fremtid
    ///   in the xref's folder that is another file is warned about.
    /// - Pipe: every part of an FJV pipe within 25 mm of a stretch that lies along it (see Along: a pipe that
    ///   only crosses a stretch is not on it), counted once: per pipe the stretches take their part in a fixed
    ///   order (by handle) from what earlier stretches left.
    /// - Component: on a stretch exactly when the crawl runs through it (TblComponent.IsOn): a spoke component
    ///   by its insertion point within 25 mm, a centreline component by two ports within 25 mm or half its
    ///   chains within 1 mm. On several, the one it runs through wins (decision 2), then the one that starts at
    ///   it, then the fixed order.
    /// - Everything else is unchanged: the rows are built exactly as in polygon mode.
    /// - Pipe metres and components on no stretch are listed (command line and IntersectResult_coverage.csv).
    /// </summary>
    internal static class TblLineMode
    {
        public const double Tolerance = 0.025;
        public const string FremtidName = "Fremtid";
        public const string CoveragePath = @"C:\Temp\IntersectResult_coverage.csv";
        public const double CoincidenceTolerance = 0.001;
        private const double AlongShare = 0.5;
        private const double GapFloor = 0.001;

        public static bool HasLines(Database db)
        {
            using Transaction tr = db.TransactionManager.StartOpenCloseTransaction();
            return db.ListOfType<Polyline>(tr).Any(p => TblBelægninger.IsStretchLayer(p.Layer));
        }

        /// <summary>The one Fremtid xref of the drawing (name or file holds "Fremtid", as NSALIGNMENTCRAWL finds it), loaded.</summary>
        public static Result<(BlockReference Xref, string Path), TblError> FremtidXref(Database db, Transaction tx)
        {
            BlockTableRecord ms = (BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            List<(BlockReference Br, BlockTableRecord Btr)> found = [];
            foreach (Oid id in ms)
            {
                if (tx.GetObject(id, OpenMode.ForRead) is not BlockReference br) continue;
                BlockTableRecord btr = (BlockTableRecord)tx.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                if (!btr.IsFromExternalReference) continue;
                if (Contains(btr.Name, FremtidName) || XrefPath(btr).Map(p => Contains(Path.GetFileNameWithoutExtension(p), FremtidName)).GetValueOrDefault(false))
                    found.Add((br, btr));
            }

            if (found.Count == 0)
                return Result.Failure<(BlockReference, string), TblError>(TblError.Drawing(
                    $"Linjetilstand: ingen Fremtid-xref (navn eller fil med \"{FremtidName}\") i tegningen. Stræk på {TblBelægninger.BareLayer}-lagene tælles på den xref, de er tegnet på."));
            if (found.Count > 1)
                return Result.Failure<(BlockReference, string), TblError>(TblError.Drawing(
                    $"Linjetilstand: {found.Count} Fremtid-xrefs ({string.Join(", ", found.Select(f => f.Btr.Name))}); stræk kan kun tælles på én."));

            (BlockReference xref, BlockTableRecord xbtr) = found[0];
            if (xbtr.XrefStatus != XrefStatus.Resolved)
                return Result.Failure<(BlockReference, string), TblError>(TblError.Drawing(
                    $"Linjetilstand: Fremtid-xref'en {xbtr.Name} er ikke indlæst ({xbtr.XrefStatus})."));
            return XrefPath(xbtr)
                .ToResult(TblError.Drawing($"Linjetilstand: Fremtid-xref'en {xbtr.Name} har ingen fil."))
                .Map(path => (xref, path));
        }

        private static bool Contains(string haystack, string needle) =>
            haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        private static Maybe<string> XrefPath(BlockTableRecord btr)
        {
            try
            {
                Database xdb = btr.GetXrefDatabase(false);
                return xdb is null || xdb.Filename.IsNoE() ? Maybe<string>.None : Maybe.From(xdb.Filename);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                return Maybe<string>.None;
            }
        }

        /// <summary>
        /// Stier.csv is no source in line mode, but a Fremtid it names in the xref's own folder is the one the
        /// project counts on: an xref of another revision there is warned about. Projects are not asked for.
        /// </summary>
        public static string StierCheck(string xrefPath)
        {
            string folder = Path.GetDirectoryName(xrefPath);
            List<(string Prj, string Etape, string Fremtid)> inFolder;
            try
            {
                inFolder = Csv.Stier.Rows
                    .Select(r => (Prj: Stier.Col(r, Stier.Columns.PrjId), Etape: Stier.Col(r, Stier.Columns.Etape),
                        Fremtid: Stier.Col(r, Stier.Columns.Fremtid)))
                    .Where(r => !r.Fremtid.IsNoE() && string.Equals(Path.GetDirectoryName(r.Fremtid), folder, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
            {
                return $"Stier.csv kunne ikke læses ({ex.Message}); kontrollen er sprunget over.";
            }

            if (inFolder.Count == 0)
                return $"Stier.csv nævner ingen Fremtid i xref'ens mappe ({folder}); kontrollen er sprunget over.";
            var same = inFolder.Where(r => string.Equals(r.Fremtid, xrefPath, StringComparison.OrdinalIgnoreCase)).ToList();
            if (same.Count > 0)
                return $"Stier.csv: xref'en er Fremtid for {string.Join(", ", same.Select(s => $"{s.Prj}/{s.Etape}"))}.";
            return $"ADVARSEL: xref'en {Path.GetFileName(xrefPath)} er ikke den Fremtid, Stier.csv nævner i samme mappe: " +
                string.Join("; ", inFolder.Select(s => $"{s.Prj}/{s.Etape} {Path.GetFileName(s.Fremtid)}")) + ".";
        }

        /// <summary>The stretches in the xref's coordinates, in the fixed order (by handle) the counting uses.</summary>
        public static List<TblLine> ReadLines(Database db, Transaction tx, Matrix3d toXref)
        {
            PropertySetManager psm = new PropertySetManager(db, PSetDefs.DefinedSets.DriOmråder);
            PSetDefs.DriOmråder def = new PSetDefs.DriOmråder();
            return db.ListOfType<Polyline>(tx)
                .Where(p => TblBelægninger.IsStretchLayer(p.Layer))
                .OrderBy(p => p.Handle.Value)
                .Select((p, i) =>
                {
                    LineString line = TblLineGeometry.ToLineString(p, toXref);
                    return new TblLine(i, p.Handle.ToString(), p.Layer,
                        psm.ReadPropertyString(p, def.Vejnavn), psm.ReadPropertyString(p, def.Vejklasse),
                        psm.ReadPropertyString(p, def.Belægning),
                        line, TblLineGeometry.Corridor(line, Tolerance), TblLineGeometry.Corridor(line, CoincidenceTolerance),
                        line.StartPoint.Coordinate, line.EndPoint.Coordinate);
                })
                .ToList();
        }

        /// <summary>
        /// The stretch a component belongs to among those it is on, and the rule that chose it: the one it runs
        /// through, then the one that starts at it, then the fixed order.
        /// </summary>
        public static (TblLine Line, string Rule) Owner(IReadOnlyList<TblLine> candidates, TblComponent component)
        {
            if (candidates.Count == 1) return (candidates[0], "one");
            List<TblLine> through = candidates.Where(l => !component.IsAt(l.Start) && !component.IsAt(l.End)).ToList();
            if (through.Count > 0) return (through[0], through.Count == 1 ? "through" : "through, fixed order");
            List<TblLine> starting = candidates.Where(l => component.IsAt(l.Start)).ToList();
            if (starting.Count > 0) return (starting[0], starting.Count == 1 ? "start" : "start, fixed order");
            return (candidates[0], "fixed order");
        }

        public sealed record PipeGap(string Handle, string Layer, string System, string Type, string Dn, double Gap, double Length);

        public sealed record LooseComponent(string Handle, string Block, string Navn, double X, double Y);

        public sealed record SystemTotal(string System, string Type, double Length, double Covered, int Components, int ComponentsCovered);

        public static void ReportCoverage(
            IReadOnlyList<PipeGap> gaps, IReadOnlyList<LooseComponent> loose, IReadOnlyList<SystemTotal> totals,
            IReadOnlyDictionary<string, int> tieRules)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            foreach (SystemTotal t in totals)
                prdDbg($"Dækning {t.System} {t.Type}: rør {t.Covered.ToString("0.00", ci)} af {t.Length.ToString("0.00", ci)} m på stræk " +
                    $"(hul {(t.Length - t.Covered).ToString("0.00", ci)} m), komponenter {t.ComponentsCovered} af {t.Components}.");
            prdDbg($"Rør med meter uden stræk: {gaps.Count} ({gaps.Sum(g => g.Gap).ToString("0.00", ci)} m). Komponenter uden stræk: {loose.Count}.");
            foreach (var (rule, count) in tieRules.OrderBy(x => x.Key))
                prdDbg($"Komponent på flere stræk, valgt efter \"{rule}\": {count}.");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Afsnit;Handle;Lag/Blok;System;Type/Navn;DN;Hul m;Rør m;X;Y");
            foreach (PipeGap g in gaps)
                sb.AppendLine($"Rør uden stræk;{g.Handle};{g.Layer};{g.System};{g.Type};{g.Dn};{g.Gap.ToString("0.000", ci)};{g.Length.ToString("0.000", ci)};;");
            foreach (LooseComponent c in loose)
                sb.AppendLine($"Komponent uden stræk;{c.Handle};{c.Block};;{c.Navn};;;;{c.X.ToString("0.000", ci)};{c.Y.ToString("0.000", ci)}");
            sb.AppendLine("Afsnit;System;Type;Rør m;Rør på stræk m;Hul m;Komponenter;Komponenter på stræk");
            foreach (SystemTotal t in totals)
                sb.AppendLine($"Total;{t.System};{t.Type};{t.Length.ToString("0.000", ci)};{t.Covered.ToString("0.000", ci)};" +
                    $"{(t.Length - t.Covered).ToString("0.000", ci)};{t.Components};{t.ComponentsCovered}");
            File.WriteAllText(CoveragePath, sb.ToString(), Encoding.UTF8);
            prdDbg($"Dækningsrapport skrevet til {CoveragePath}");
        }

        public static bool IsGap(double length) => length > GapFloor;

        /// <summary>
        /// The pieces of a pipe's part in a line's corridor that lie ALONG the line: at least half of the piece
        /// within 1 mm of it. A line crawled on a pipe coincides with it to well under a millimetre, while a pipe
        /// that only crosses the line lies within 1 mm of it for a few millimetres of the 50 it spends in the
        /// corridor. The same test as TBLCRAWL's cut rule and partner runs.
        /// </summary>
        public static List<LineString> Along(Geometry near, TblLine line) =>
            Parts(near).Where(p => p.Intersection(line.Coincident).Length >= AlongShare * p.Length).ToList();

        public static IEnumerable<LineString> Parts(Geometry geometry)
        {
            for (int i = 0; i < geometry.NumGeometries; i++)
                if (geometry.GetGeometryN(i) is LineString part && !part.IsEmpty && part.Length > 0)
                    yield return part;
        }
    }

    /// <summary>
    /// Curves as NTS line strings for line mode. Arcs are densified to a chord error of 0.5 mm (the polygon
    /// mode's 0.1 rad samples miss an R 100 m arc by 12 cm, wider than the 25 mm corridor). The same rule
    /// as IntersectUtilities' TblStretchGeometry, which draws the stretches.
    /// </summary>
    internal static class TblLineGeometry
    {
        private const double MaxSagitta = 0.0005;
        private static readonly GeometryFactory Factory = new GeometryFactory();
        private static readonly BufferParameters FlatEnds =
            new BufferParameters(8, EndCapStyle.Flat, NetTopologySuite.Operation.Buffer.JoinStyle.Round, 5.0);

        public static LineString ToLineString(Polyline pl) => ToLineString(pl, Matrix3d.Identity);

        public static LineString ToLineString(Polyline pl, Matrix3d transform)
        {
            List<Coordinate> coordinates = new List<Coordinate>();
            void Add(double x, double y)
            {
                Point3d p = new Point3d(x, y, 0.0).TransformBy(transform);
                Coordinate c = new Coordinate(p.X, p.Y);
                if (coordinates.Count == 0 || !coordinates[^1].Equals2D(c)) coordinates.Add(c);
            }

            int n = pl.NumberOfVertices;
            for (int i = 0; i < n; i++)
            {
                Point2d p0 = pl.GetPoint2dAt(i);
                Add(p0.X, p0.Y);
                if (i == n - 1) break;
                Point2d p1 = pl.GetPoint2dAt(i + 1);
                double bulge = pl.GetBulgeAt(i);
                double chord = p0.GetDistanceTo(p1);
                if (bulge == 0.0 || chord == 0.0) continue;

                // Positive bulge = counter-clockwise; the centre sits chord(1 - b²)/(4b) left of the chord's midpoint.
                double radius = chord * (1 + bulge * bulge) / (4 * Math.Abs(bulge));
                double sweep = 4 * Math.Atan(bulge);
                Vector2d along = p0.GetVectorTo(p1) / chord;
                Vector2d left = new Vector2d(-along.Y, along.X);
                Point2d centre = new Point2d((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2) + left * (chord * (1 - bulge * bulge) / (4 * bulge));
                double start = Math.Atan2(p0.Y - centre.Y, p0.X - centre.X);
                double step = MaxSagitta < radius ? 2 * Math.Acos(1 - MaxSagitta / radius) : Math.Abs(sweep);
                int count = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / step));
                for (int k = 1; k < count; k++)
                {
                    double a = start + sweep * k / count;
                    Add(centre.X + radius * Math.Cos(a), centre.Y + radius * Math.Sin(a));
                }
            }

            if (coordinates.Count == 1) coordinates.Add(coordinates[0].Copy());
            return Factory.CreateLineString(coordinates.ToArray());
        }

        public static Geometry Corridor(Geometry line, double halfWidth) => line.Buffer(halfWidth, FlatEnds);

        public static Geometry Collect(IEnumerable<LineString> parts) => Factory.BuildGeometry(parts.Cast<Geometry>().ToList());

        /// <summary>
        /// A curve of a block definition as a line string in the block reference's world: sampled every centimetre
        /// in the definition (a component's chains are at most a few metres) and each point mapped, which holds for
        /// any block transform, mirrored or scaled.
        /// </summary>
        public static LineString Sampled(Curve curve, Matrix3d toWorld)
        {
            double length = curve.GetDistanceAtParameter(curve.EndParam);
            int steps = Math.Max(2, (int)Math.Ceiling(length / 0.01));
            IEnumerable<Point3d> points = curve is Line || length == 0
                ? new[] { curve.StartPoint, curve.EndPoint }
                : Enumerable.Range(0, steps + 1).Select(i => curve.GetPointAtDist(Math.Min(length, length * i / steps)));
            return Factory.CreateLineString(points
                .Select(p => p.TransformBy(toWorld))
                .Select(p => new Coordinate(p.X, p.Y))
                .ToArray());
        }

        public static NetTopologySuite.Geometries.Point Point(Coordinate c) => Factory.CreatePoint(c);
    }

    public partial class DimensioneringExtension
    {
        /// <summary>
        /// gatherintersectdata for a drawing with TBL stretches (TblLineMode). Same rows, same rules after the
        /// counting; null when it cannot run (the reason is on the command line), as gatherintersectdata.
        /// </summary>
        private HashSet<IntersectResult> gatherlinemodedata(
            Database localDb, bool inheritWeldSerie, bool bendAngleInName, bool fixComponentData)
        {
            try
            {
                return gatherlinemodedataGuarded(localDb, inheritWeldSerie, bendAngleInName, fixComponentData);
            }
            catch (System.Exception ex)
            {
                //Last resort, as in polygon mode: a bug, not a drawing problem.
                prdDbg(ex);
                return null;
            }
        }

        private HashSet<IntersectResult> gatherlinemodedataGuarded(
            Database localDb, bool inheritWeldSerie, bool bendAngleInName, bool fixComponentData)
        {
            using Transaction tx = localDb.TransactionManager.StartTransaction();
            Result<HashSet<IntersectResult>, TblError> result = TblLineMode.FremtidXref(localDb, tx)
                .Bind(xref => ReadFremtid(xref.Path).Map(fremDb => (xref.Xref, xref.Path, FremDb: fremDb)))
                .Map(source =>
                {
                    using Database fremDb = source.FremDb;
                    List<TblLine> lines = TblLineMode.ReadLines(localDb, tx, source.Xref.BlockTransform.Inverse());
                    string perLayer = string.Join(", ", lines.GroupBy(l => l.Layer, StringComparer.OrdinalIgnoreCase)
                        .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}"));
                    prdDbg($"Linjetilstand: {lines.Count} stræk ({perLayer}); Fremtid fra xref'en {source.Path}.");
                    foreach (TblLine line in lines.Where(l => l.LayerMismatch))
                        prdDbg($"ADVARSEL: stræk {line.Handle} ligger på {line.Layer}, men PS Belægning er \"{line.Belægning}\". " +
                            "Eksporten tæller efter PS.");
                    prdDbg(TblLineMode.StierCheck(source.Path));
                    using Transaction fremTx = fremDb.TransactionManager.StartTransaction();
                    HashSet<IntersectResult> rows = CountOnLines(lines, fremDb, fremTx, inheritWeldSerie, bendAngleInName, fixComponentData);
                    fremTx.Abort();
                    return rows;
                });
            tx.Commit();
            return result.Match(
                rows => rows,
                error =>
                {
                    prdDbg(error.Message);
                    return null;
                });
        }

        private static Result<Database, TblError> ReadFremtid(string path)
        {
            Database fremDb = new Database(false, true);
            try
            {
                fremDb.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, "");
                return Result.Success<Database, TblError>(fremDb);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                fremDb.Dispose();
                return Result.Failure<Database, TblError>(TblError.Host($"Linjetilstand: kunne ikke læse {path}", ex));
            }
        }

        private static HashSet<IntersectResult> CountOnLines(
            List<TblLine> lines, Database fremDb, Transaction fremTx,
            bool inheritWeldSerie, bool bendAngleInName, bool fixComponentData)
        {
            HashSet<IntersectResult> allResults = new HashSet<IntersectResult>();
            HashSet<Polyline> pipes = fremDb.GetFjvPipes(fremTx);
            HashSet<BlockReference> comps = fremDb.HashSetOfType<BlockReference>(fremTx);

            //The same serie fallbacks as polygon mode (gatherintersectdata).
            var pipesWithExtents = pipes
                .Where(p => p.Bounds.HasValue)
                .Select(p => (Pipe: p, Ext: p.Bounds.Value))
                .ToArray();
            var componentPorts = new Lazy<(Point3d Port, string Serie, string SysNavn, Oid Owner)[]>(
                () => ReadComponentPortSeries(comps, pipesWithExtents));
            var weldSerieCache = new Dictionary<Oid, string>();
            var skippedBlocks = new Dictionary<string, int>();
            var componentNotes = new List<string>();

            var gaps = new List<TblLineMode.PipeGap>();
            var loose = new List<TblLineMode.LooseComponent>();
            var pipeTotals = new Dictionary<(string System, string Type), (double Length, double Covered)>();
            var compTotals = new Dictionary<(string System, string Type), (int All, int Covered)>();
            var tieRules = new Dictionary<string, int>();

            foreach (Polyline pipe in pipes.OrderBy(p => p.Handle.Value))
            {
                LineString pipeLine = TblLineGeometry.ToLineString(pipe);
                Geometry remaining = pipeLine;
                foreach (TblLine line in lines)
                {
                    if (remaining.IsEmpty) break;
                    if (!line.Corridor.EnvelopeInternal.Intersects(remaining.EnvelopeInternal)) continue;
                    Geometry near = remaining.Intersection(line.Corridor);
                    if (near.Length <= 0) continue;
                    List<LineString> along = TblLineMode.Along(near, line);
                    if (along.Count == 0) continue;
                    //A pipe crossing the line passes 50 mm of its corridor without lying on it: it stays uncounted.
                    List<LineString> across = TblLineMode.Parts(near).Except(along).ToList();
                    remaining = remaining.Difference(line.Corridor);
                    if (across.Count > 0) remaining = remaining.Union(TblLineGeometry.Collect(across));
                    double partLength = along.Sum(p => p.Length);

                    IntersectResultPipe irp = new IntersectResultPipe();
                    irp.Vejnavn = line.Vejnavn;
                    irp.Vejklasse = line.Vejklasse;
                    irp.Belægning = line.Belægning;
                    irp.DN1 = GetPipeDN(pipe).ToString();
                    irp.System = GetPipeType(pipe, true).ToString();
                    irp.Serie = GetPipeSeriesV2(pipe, true).ToString();
                    irp.Length = partLength;
                    irp.SystemType = GetPipeSystem(pipe).ToString();
                    double stdLength = GetPipeStdLength(pipe);
                    irp.StdLength = stdLength > 0
                        ? stdLength.ToString("0.##", CultureInfo.InvariantCulture)
                        : "";
                    allResults.Add(irp);
                }

                var key = (GetPipeSystem(pipe).ToString(), GetPipeType(pipe).ToString());
                var sum = pipeTotals.GetValueOrDefault(key);
                pipeTotals[key] = (sum.Length + pipeLine.Length, sum.Covered + pipeLine.Length - remaining.Length);
                if (TblLineMode.IsGap(remaining.Length))
                    gaps.Add(new TblLineMode.PipeGap(pipe.Handle.ToString(), pipe.Layer, GetPipeSystem(pipe).ToString(),
                        GetPipeType(pipe).ToString(), GetPipeDN(pipe).ToString(), remaining.Length, pipeLine.Length));
            }

            foreach (BlockReference br in comps.OrderBy(b => b.Handle.Value))
            {
                TblComponent component = TblComponent.Read(br, fremTx);
                List<TblLine> candidates = lines.Where(component.IsOn).ToList();
                string navn = br.ReadDynamicCsvProperty(DynamicProperty.TBLNavn, true);
                var compKey = (br.ReadDynamicCsvProperty(DynamicProperty.SysNavn, true), br.ReadDynamicCsvProperty(DynamicProperty.System, true));
                if (!navn.IsNoE())
                {
                    var c = compTotals.GetValueOrDefault(compKey);
                    compTotals[compKey] = (c.All + 1, c.Covered + (candidates.Count > 0 ? 1 : 0));
                }

                if (candidates.Count == 0)
                {
                    if (!navn.IsNoE())
                        loose.Add(new TblLineMode.LooseComponent(br.Handle.ToString(), br.RealName(), navn, br.Position.X, br.Position.Y));
                    continue;
                }

                (TblLine owner, string rule) = TblLineMode.Owner(candidates, component);
                if (candidates.Count > 1) tieRules[rule] = tieRules.GetValueOrDefault(rule) + 1;

                IntersectResultComponent irc = new IntersectResultComponent();
                irc.Vejnavn = owner.Vejnavn;
                irc.Vejklasse = owner.Vejklasse;
                irc.Belægning = owner.Belægning;
                irc.Navn = navn;
                if (fixComponentData && irc.Navn.IsNoE())
                {
                    string blockName = br.RealName();
                    skippedBlocks[blockName] = skippedBlocks.GetValueOrDefault(blockName) + 1;
                    continue;
                }
                if (bendAngleInName) irc.Navn = NameWithBendAngle(br, irc.Navn);
                irc.DN1 = br.ReadDynamicCsvProperty(DynamicProperty.DN1, true);
                irc.DN2 = br.ReadDynamicCsvProperty(DynamicProperty.DN2, true);
                irc.System = br.ReadDynamicCsvProperty(DynamicProperty.System, true);
                irc.Serie = br.ReadDynamicCsvProperty(DynamicProperty.Serie, true);
                irc.SystemType = br.ReadDynamicCsvProperty(DynamicProperty.SysNavn, true);

                bool isWeld = br.ReadDynamicCsvProperty(DynamicProperty.Type, false) == "Svejsning";
                if (inheritWeldSerie && irc.Serie.IsNoE() && isWeld)
                    irc.Serie = GetSerieFromNeighbour(br, pipesWithExtents, componentPorts, weldSerieCache);
                if (fixComponentData && !isWeld)
                    FixComponentData(br, irc, pipesWithExtents, componentPorts, componentNotes);

                if (irc.System == "Frem" || irc.System == "Retur") irc.System = "Enkelt";

                allResults.Add(irc);
            }

            foreach (var (blockName, count) in skippedBlocks)
                prdDbg($"Udeladt, ingen række i FJV Dynamiske Komponenter.csv: {blockName} ({count} stk.)");
            foreach (string note in componentNotes) prdDbg(note);

            var totals = pipeTotals.Keys.Union(compTotals.Keys).OrderBy(k => k.System).ThenBy(k => k.Type)
                .Select(k => new TblLineMode.SystemTotal(k.System, k.Type,
                    pipeTotals.GetValueOrDefault(k).Length, pipeTotals.GetValueOrDefault(k).Covered,
                    compTotals.GetValueOrDefault(k).All, compTotals.GetValueOrDefault(k).Covered))
                .ToList();
            TblLineMode.ReportCoverage(gaps, loose, totals, tieRules);
            return allResults;
        }
    }
}
