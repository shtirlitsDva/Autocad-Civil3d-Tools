using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using CSharpFunctionalExtensions;

using IntersectUtilities.Tbl;
using IntersectUtilities.UtilsCommon;

using NetTopologySuite.Geometries;

using static IntersectUtilities.UtilsCommon.Utils;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Result = CSharpFunctionalExtensions.Result;

namespace IntersectUtilities.NSTBL
{
    /// <summary>What an xref of the Fremtid holds for the export.</summary>
    internal enum TblXrefKind
    {
        /// <summary>TBLCRAWL stretches on 0-TBL-STRÆK or 0-TBL-STRÆK-* (the VEJKLASSE drawing): line mode's rules.</summary>
        Lines,

        /// <summary>Closed polylines on 0-OMRÅDER-OK (an older område drawing): polygon mode's rules.</summary>
        Areas,
    }

    /// <summary>An area of polygon mode in the counted drawing's coordinates, with its tags.</summary>
    internal sealed record TblArea(Polygon Polygon, string Vejnavn, string Vejklasse, string Belægning);

    /// <summary>
    /// TBLEXPORTV2 run in the Fremtid file itself (user, 03/11): the stretches, or an older drawing's område polygons,
    /// come through the xref that holds them, moved into this drawing by the xref's transform, and are counted on this
    /// drawing's own pipes and components, read from memory so unsaved edits count. Nothing asks for a project.
    /// - Only a drawing with FJV pipes of its own is a Fremtid. Anywhere else the old modes run unchanged (own stretches
    ///   and a Fremtid xref, or own polygons and Stier.csv), so an område drawing that xrefs another one is not misread.
    /// - The source is the one loaded xref in model space whose own model space holds stretches or polygons. Xrefs nested
    ///   in it (the Fremtid the VEJKLASSE drawing overlays, or attaches) are not looked at. It must be inserted once,
    ///   flat and with one scale in X and Y, and hold one kind only; two sources, of one kind or both, stop the export.
    /// - The xref comes first, then the old modes. Own stretches beside it stop the export; own polygons (left from the
    ///   polygon era) are reported and not counted.
    /// - Nothing is written: the tags are read without attaching a set, and this drawing's transaction is aborted.
    /// </summary>
    internal sealed record TblXrefSource(
        string XrefName, string XrefPath, TblXrefKind Kind, List<TblLine> Lines, List<TblArea> Areas, int OwnAreas)
    {
        public const string AreaLayer = "0-OMRÅDER-OK";

        /// <summary>The xref to count on; None when the old modes run; the reason when the export cannot run.</summary>
        public static Result<Maybe<TblXrefSource>, TblError> Find(Database db)
        {
            try
            {
                using Transaction tx = db.TransactionManager.StartTransaction();
                Result<Maybe<TblXrefSource>, TblError> found = FindIn(db, tx);
                tx.Commit();
                return found;
            }
            catch (System.Exception ex)
            {
                return Result.Failure<Maybe<TblXrefSource>, TblError>(TblError.Host("Xref-kilde: tegningens xrefs kunne ikke læses", ex));
            }
        }

        // An xref's database shares this drawing's transaction stack: while the xref's transaction is open it is the top
        // one, so this drawing's transaction is not used inside it.
        private static Result<Maybe<TblXrefSource>, TblError> FindIn(Database db, Transaction tx)
        {
            if (db.GetFjvPipes(tx).Count == 0) return Result.Success<Maybe<TblXrefSource>, TblError>(Maybe<TblXrefSource>.None);

            List<Polyline> own = db.ListOfType<Polyline>(tx);
            int ownLines = own.Count(p => TblBelægninger.IsStretchLayer(p.Layer));
            int ownAreas = own.Count(p => p.Layer == AreaLayer);

            var xrefs = db.HashSetOfType<BlockReference>(tx)
                .GroupBy(br => br.BlockTableRecord)
                .Select(inserts => (Record: (BlockTableRecord)tx.GetObject(inserts.Key, OpenMode.ForRead), Inserts: inserts.ToList()))
                .Where(x => x.Record.IsFromExternalReference)
                .ToList();
            List<string> notLoaded = xrefs
                .Where(x => x.Record.XrefStatus != XrefStatus.Resolved)
                .Select(x => $"{x.Record.Name} ({StatusText(x.Record.XrefStatus)})")
                .ToList();
            var holders = xrefs
                .Where(x => x.Record.XrefStatus == XrefStatus.Resolved)
                .Select(x => (x.Record.Name, Db: x.Record.GetXrefDatabase(false), x.Inserts))
                .Where(x => x.Db != null)
                .Select(x => (x.Name, x.Db, x.Inserts, Holds: Holds(x.Db)))
                .Where(x => x.Holds.Lines || x.Holds.Areas)
                .ToList();

            if (holders.Count == 0)
            {
                string unloaded = notLoaded.Count == 0 ? "" : $" Ikke indlæst: {string.Join(", ", notLoaded)}.";
                if (ownLines == 0 && ownAreas == 0)
                    return Fail($"Ingen TBL-stræk eller områder: tegningen har FJV-rør, men ingen indlæst xref i modelrummet har " +
                        $"polylinjer på {TblBelægninger.BareLayer}* eller {AreaLayer}.{unloaded} Indsæt VEJKLASSE- eller områdetegningen " +
                        "som xref, og kør kommandoen igen.");
                if (notLoaded.Count > 0)
                    prdDbg($"ADVARSEL: tegningens egne stræk eller områder tælles.{unloaded} Har en af de ikke indlæste xrefs " +
                        "stræk eller områder, tælles de ikke med.");
                return Result.Success<Maybe<TblXrefSource>, TblError>(Maybe<TblXrefSource>.None);
            }

            if (holders.Count > 1)
            {
                string Named(TblXrefKind kind)
                {
                    List<string> names = holders.Where(h => h.Holds.Kind == kind).Select(h => h.Name).ToList();
                    return names.Count == 1 ? $"xref'en {names[0]}" : $"xref'erne {string.Join(", ", names)}";
                }
                return Fail(holders.Select(h => h.Holds.Kind).Distinct().Count() > 1
                    ? $"Både stræk og områder: {Named(TblXrefKind.Lines)} har TBL-stræk, og {Named(TblXrefKind.Areas)} har " +
                      $"områder på {AreaLayer}. Der tælles kun på én kilde; fjern eller unload alle undtagen én, og kør kommandoen igen."
                    : $"Flere xrefs har {KindText(holders[0].Holds.Kind)}: {string.Join(", ", holders.Select(h => h.Name))}. " +
                      "Fjern eller unload alle undtagen én, og kør kommandoen igen.");
            }

            var (name, xdb, inserts, holds) = holders[0];
            if (holds.Lines && holds.Areas)
                return Fail($"Xref'en {name} har både TBL-stræk og områder på {AreaLayer}. Der tælles kun på én slags; slet den " +
                    $"anden i {name}, og kør kommandoen igen.");
            if (inserts.Count > 1)
                return Fail($"Xref'en {name} er indsat {inserts.Count} gange i modelrummet; den må kun være der én gang. " +
                    "Slet de ekstra indsættelser, og kør kommandoen igen.");
            Matrix3d toHost = inserts[0].BlockTransform;
            if (!IsFlatAndUniform(toHost))
                return Fail($"Xref'en {name} er indsat med forskellig skala i X og Y eller drejet ud af XY-planet, så kan " +
                    $"{(holds.Kind == TblXrefKind.Lines ? "TBL-strækkene" : "områderne")} ikke føres over på rørene. Indsæt den " +
                    "vandret og med ens skala i X og Y, og kør kommandoen igen.");
            if (ownLines > 0)
                return Fail($"Tegningen har selv {ownLines} TBL-stræk, og xref'en {name} har {KindText(holds.Kind)}. Der tælles kun " +
                    "på én kilde; slet tegningens egne stræk, eller fjern xref'en, og kør kommandoen igen.");

            string path = xdb.Filename;
            using Transaction xtx = xdb.TransactionManager.StartTransaction();
            Result<Maybe<TblXrefSource>, TblError> source = holds.Kind == TblXrefKind.Lines
                ? Result.Success<Maybe<TblXrefSource>, TblError>(Maybe.From(new TblXrefSource(
                    name, path, TblXrefKind.Lines, TblLineMode.ReadXrefLines(xdb, xtx, toHost), new List<TblArea>(), ownAreas)))
                : ReadAreas(xdb, xtx, toHost, name)
                    .Map(areas => Maybe.From(new TblXrefSource(name, path, TblXrefKind.Areas, new List<TblLine>(), areas, ownAreas)));
            xtx.Commit();
            return source;
        }

        private static Result<Maybe<TblXrefSource>, TblError> Fail(string message) =>
            Result.Failure<Maybe<TblXrefSource>, TblError>(TblError.Drawing(message));

        private static string KindText(TblXrefKind kind) => kind == TblXrefKind.Lines ? "TBL-stræk" : $"områder på {AreaLayer}";

        private static string StatusText(XrefStatus status) => status switch
        {
            XrefStatus.Unloaded => "unloaded",
            XrefStatus.FileNotFound => "filen findes ikke",
            XrefStatus.Unresolved => "ikke fundet",
            _ => status.ToString(),
        };

        /// <summary>
        /// The xref's X and Y axes stay in the XY plane, at right angles and of one length: a move, a turn, a mirror and a
        /// uniform scale are fine, the Z scale does not matter (the stretches and areas are read flat).
        /// </summary>
        private static bool IsFlatAndUniform(Matrix3d toHost)
        {
            Vector3d x = Vector3d.XAxis.TransformBy(toHost);
            Vector3d y = Vector3d.YAxis.TransformBy(toHost);
            double tolerance = 1e-9 * Math.Max(x.Length, y.Length);
            return x.Length > 0 && Math.Abs(x.Length - y.Length) <= tolerance && Math.Abs(x.DotProduct(y)) <= tolerance * x.Length
                && Math.Abs(x.Z) <= tolerance && Math.Abs(y.Z) <= tolerance;
        }

        private static (bool Lines, bool Areas, TblXrefKind Kind) Holds(Database xdb)
        {
            using Transaction xtx = xdb.TransactionManager.StartTransaction();
            List<string> layers = xdb.ListOfType<Polyline>(xtx).Select(p => TblLineMode.LocalLayer(p.Layer)).ToList();
            xtx.Commit();
            bool lines = layers.Any(TblBelægninger.IsStretchLayer);
            return (lines, layers.Any(l => l == AreaLayer), lines ? TblXrefKind.Lines : TblXrefKind.Areas);
        }

        /// <summary>The xref's areas in model space order, as polygon mode reads its own; an open one stops the export, as there.</summary>
        private static Result<List<TblArea>, TblError> ReadAreas(Database xdb, Transaction xtx, Matrix3d toHost, string xrefName)
        {
            PSetDefs.DriOmråder def = new PSetDefs.DriOmråder();
            List<TblArea> areas = new List<TblArea>();
            foreach (Polyline p in xdb.ListOfType<Polyline>(xtx).Where(p => TblLineMode.LocalLayer(p.Layer) == AreaLayer))
            {
                if (!p.Closed)
                    return Result.Failure<List<TblArea>, TblError>(TblError.Drawing(
                        $"Områdetilstand: området {p.Handle} i xref'en {xrefName} er ikke lukket. Luk det, og kør kommandoen igen."));
                if (p.NumberOfVertices < 3)
                    return Result.Failure<List<TblArea>, TblError>(TblError.Drawing(
                        $"Områdetilstand: området {p.Handle} i xref'en {xrefName} har færre end 3 hjørner. Ret eller slet det, og " +
                        "kør kommandoen igen."));
                areas.Add(new TblArea(ToHostPolygon(p, toHost),
                    ReadTag(p, xtx, def.Vejnavn), ReadTag(p, xtx, def.Vejklasse), ReadTag(p, xtx, def.Belægning)));
            }

            return Result.Success<List<TblArea>, TblError>(areas);
        }

        /// <summary>NTSConversion.ConvertClosedPlineToNTSPolygon with every point moved by the xref's transform.</summary>
        private static Polygon ToHostPolygon(Polyline pl, Matrix3d toHost)
        {
            Coordinate At(double x, double y)
            {
                Point3d p = new Point3d(x, y, 0.0).TransformBy(toHost);
                return new Coordinate(p.X, p.Y);
            }

            var points = new List<Coordinate>();
            for (int i = 0; i < pl.NumberOfVertices; i++)
                points.Add(At(pl.GetPoint2dAt(i).X, pl.GetPoint2dAt(i).Y));
            points.Add(At(pl.GetPoint3dAt(0).X, pl.GetPoint3dAt(0).Y));
            return new Polygon(new LinearRing(points.ToArray()));
        }

        /// <summary>
        /// A DriOmråder value read without writing: PropertySetManager attaches a missing set, which an xref's database
        /// must not get. "" when the entity has no DriOmråder set or the set no value, as an attached set's default reads.
        /// </summary>
        public static string ReadTag(Entity ent, Transaction tx, PSetDefs.Property property)
        {
            string setName = PSetDefs.DefinedSets.DriOmråder.ToString();
            foreach (Oid id in PropertyDataServices.GetPropertySets(ent))
            {
                PropertySet set = (PropertySet)tx.GetObject(id, OpenMode.ForRead);
                if (set.PropertySetDefinitionName != setName) continue;
                return set.GetAt(set.PropertyNameToId(property.Name))?.ToString() ?? "";
            }

            return "";
        }

        /// <summary>The blocks that are not xref inserts, in their order.</summary>
        public static HashSet<BlockReference> WithoutXrefs(HashSet<BlockReference> blocks, Transaction tx) =>
            blocks.Where(br => !((BlockTableRecord)tx.GetObject(br.BlockTableRecord, OpenMode.ForRead)).IsFromExternalReference)
                .ToHashSet();
    }

    public partial class DimensioneringExtension
    {
        /// <summary>
        /// gatherintersectdata in the Fremtid file itself (TblXrefSource). False when the old modes run; true with the
        /// rows, or null when the export cannot run (the reason is on the command line).
        /// </summary>
        private bool trygatherxrefdata(
            Database localDb, bool inheritWeldSerie, bool bendAngleInName, bool fixComponentData, out HashSet<IntersectResult> rows)
        {
            Result<Maybe<TblXrefSource>, TblError> found = TblXrefSource.Find(localDb);
            if (found.IsFailure)
            {
                prdDbg(found.Error.Message);
                rows = null;
                return true;
            }

            if (found.Value.HasNoValue)
            {
                rows = null;
                return false;
            }

            rows = gatherxrefdata(localDb, found.Value.Value, inheritWeldSerie, bendAngleInName, fixComponentData);
            return true;
        }

        private HashSet<IntersectResult> gatherxrefdata(
            Database localDb, TblXrefSource source, bool inheritWeldSerie, bool bendAngleInName, bool fixComponentData)
        {
            try
            {
                string here = Path.GetFileName(localDb.Filename);
                bool lines = source.Kind == TblXrefKind.Lines;
                if (source.OwnAreas > 0)
                    prdDbg($"Tegningens egne {source.OwnAreas} områder på {TblXrefSource.AreaLayer} tælles ikke; " +
                        $"{(lines ? "strækkene" : "områderne")} fra xref'en {source.XrefName} bruges.");

                using Transaction tx = localDb.TransactionManager.StartTransaction();
                //An xref is no component. Here the xrefs are loaded, and the component rules would read each one's whole
                //drawing for ports. The old modes read the Fremtid from disk, where an xref insert is an empty block, and
                //keep it among the components as before (FixComponentData leaves it out as "Udeladt").
                HashSet<BlockReference> comps = TblXrefSource.WithoutXrefs(localDb.HashSetOfType<BlockReference>(tx), tx);
                HashSet<IntersectResult> rows;
                if (lines)
                {
                    prdDbg($"Linjetilstand: {source.Lines.Count} stræk ({TblLineMode.PerLayer(source.Lines)}) fra xref'en " +
                        $"{source.XrefName} ({source.XrefPath}); rør og komponenter fra denne tegning ({here}).");
                    TblLineMode.WarnLayerMismatch(source.Lines);
                    rows = CountOnLines(source.Lines, localDb, tx, comps, inheritWeldSerie, bendAngleInName, fixComponentData);
                }
                else
                {
                    prdDbg($"Områdetilstand: {source.Areas.Count} områder fra xref'en {source.XrefName} ({source.XrefPath}); " +
                        $"rør og komponenter fra denne tegning ({here}).");
                    AreaCounter counter = new AreaCounter(localDb.GetFjvPipes(tx), comps,
                        inheritWeldSerie, bendAngleInName, fixComponentData);
                    foreach (TblArea area in source.Areas)
                        counter.Count(area.Polygon, () => (area.Vejnavn, area.Vejklasse, area.Belægning));
                    counter.Report();
                    rows = counter.Results;
                }

                //Only read: this drawing is left as it was.
                tx.Abort();
                return rows;
            }
            catch (System.Exception ex)
            {
                //Last resort, as in the other modes: a bug, not a drawing problem.
                prdDbg(ex);
                return null;
            }
        }
    }
}
