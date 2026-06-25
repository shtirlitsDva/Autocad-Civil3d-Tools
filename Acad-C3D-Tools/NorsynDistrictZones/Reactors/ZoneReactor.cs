using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NetTopologySuite.Geometries;

using NorsynDistrictZones.Acad;
using NorsynDistrictZones.Model;
using NorsynDistrictZones.Pricing;
using NorsynDistrictZones.Topology;

using AcPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace NorsynDistrictZones.Reactors;

/// <summary>
/// Per-document automatic editing. Collects polylines drawn during a command and,
/// at command-end (never mid-command), classifies each against the live subdivision:
/// closed-outside → new zone, closed-inside → hole + sub-zone, open-splitting → split.
/// Everything happens in ONE transaction so a single Undo reverts the whole gesture;
/// any error aborts cleanly. Invalid cut attempts are flagged in transient red.
/// </summary>
internal sealed class ZoneReactor : IDisposable
{
    private readonly Document _doc;
    private readonly Database _db;
    private readonly NewCurveCollector _collector = new();
    private readonly InvalidPolylineMarker _marker = new();
    private readonly Random _rng = new();
    private bool _busy;

    public ZoneReactor(Document doc)
    {
        _doc = doc;
        _db = doc.Database;
        _db.ObjectAppended += _collector.OnObjectAppended;
        _doc.CommandEnded += OnCommandEnded;
        _doc.CommandCancelled += OnCommandCancelled;
    }

    public void Dispose()
    {
        try { _db.ObjectAppended -= _collector.OnObjectAppended; } catch { }
        try { _doc.CommandEnded -= OnCommandEnded; } catch { }
        try { _doc.CommandCancelled -= OnCommandCancelled; } catch { }
        _marker.Dispose();
        ZoneSession.Forget(_db);
    }

    private void OnCommandCancelled(object? sender, CommandEventArgs e) => _collector.Clear();

    private void OnCommandEnded(object? sender, CommandEventArgs e)
    {
        if (_busy) { return; }
        var ids = _collector.Drain();
        if (ids.Count == 0) return;

        _busy = true;
        try { Process(ids); }
        catch (System.Exception ex) { _doc.Editor.WriteMessage($"\nNDZ reactor error: {ex.Message}\n"); }
        finally { _busy = false; }
    }

    private void Process(IReadOnlyList<ObjectId> ids)
    {
        ZoneSession session = ZoneSession.For(_db);
        using Transaction tx = _db.TransactionManager.StartTransaction();
        var ms = (BlockTableRecord)tx.GetObject(
            SymbolUtilityServices.GetBlockModelSpaceId(_db), OpenMode.ForWrite);

        // Lazily reconstruct the session from persisted zones (drawing reopened, or
        // session cleared after a grip edit) so classification sees existing faces.
        if (session.Entries.Count == 0)
            foreach (var (cid, f) in ZoneReader.ReadAll(_db, tx))
                session.Add(cid, f);

        IReadOnlyList<PipeSegment> pipes = PipeReader.ReadFromXrefs(_db, tx, null);
        PipePriceCatalog catalog = CatalogStore.GetActive(_db);

        bool changed = false;
        foreach (ObjectId id in ids)
        {
            if (id.IsErased) continue;
            if (tx.GetObject(id, OpenMode.ForRead) is not AcPolyline pl) continue;
            if (pl.Layer == ZoneRenderer.ZoneLayer) continue; // ignore our own render output

            changed |= Handle(tx, ms, pl, session, pipes, catalog);
        }

        if (changed) tx.Commit();
        else tx.Abort();
    }

    private bool Handle(
        Transaction tx, BlockTableRecord ms, AcPolyline pl,
        ZoneSession session, IReadOnlyList<PipeSegment> pipes, PipePriceCatalog catalog)
    {
        if (pl.Closed)
        {
            Polygon? poly = AcadNts.ToPolygon(pl, Matrix3d.Identity);
            if (poly is null) return false;

            ZoneSession.Entry? parent = session.FaceContaining(poly);
            if (parent is null)
            {
                // Closed, outside every face → new top-level zone.
                ZoneFace face = NewFace(poly, session);
                RenderAndRegister(tx, ms, face, session, catalog, pipes);
                Erase(tx, pl);
                return true;
            }

            // Closed, inside a face → cut a hole and create the sub-zone.
            var cut = ZoneGeometryOps.CutHole(parent.Face.Polygon, poly);
            if (cut is null) return false;
            parent.Face.Polygon = cut.Value.ParentWithHole;
            ReRender(tx, ms, parent, catalog, pipes);

            ZoneFace sub = NewFace(cut.Value.SubFace, session);
            RenderAndRegister(tx, ms, sub, session, catalog, pipes);
            Erase(tx, pl);
            return true;
        }

        // Open polyline → try to split the face it crosses edge-to-edge.
        LineString cutLine = AcadNts.ToLineString(pl, Matrix3d.Identity);
        bool touchedAFace = false;
        foreach (ZoneSession.Entry entry in session.Entries.ToList())
        {
            if (!entry.Face.Polygon.Intersects(cutLine)) continue;
            touchedAFace = true;

            IReadOnlyList<Polygon> faces = ZoneGeometryOps.SplitByLine(entry.Face.Polygon, cutLine);
            if (faces.Count < 2) continue;

            // Keep identity (id/number/colour) on the first piece; mint the rest.
            Erase(tx, entry.Container);
            session.Remove(entry);

            var kept = new ZoneFace(entry.Face.Id, faces[0])
            { Number = entry.Face.Number, Name = entry.Face.Name, ColorArgb = entry.Face.ColorArgb };
            RenderAndRegister(tx, ms, kept, session, catalog, pipes);
            for (int i = 1; i < faces.Count; i++)
                RenderAndRegister(tx, ms, NewFace(faces[i], session), session, catalog, pipes);

            Erase(tx, pl);
            return true;
        }

        // Touched a face but produced no clean split → flag it AND say why; otherwise
        // stay silent (don't harass every unrelated polyline the user draws).
        if (touchedAFace)
        {
            _marker.Show(pl);
            _doc.Editor.WriteMessage(
                "\nNDZ: that line touched a zone but did not divide it (marked red). " +
                "To split a zone, draw an OPEN polyline that runs edge-to-edge — both ends " +
                "on (or just across) the zone boundary, crossing the interior between them.\n");
        }
        return false;
    }

    private ZoneFace NewFace(Polygon poly, ZoneSession session) =>
        new(new ZoneId(Guid.NewGuid()), poly)
        {
            Number = session.NextNumber(),
            ColorArgb = RandomColor(),
            Name = string.Empty,
        };

    private void RenderAndRegister(
        Transaction tx, BlockTableRecord ms, ZoneFace face,
        ZoneSession session, PipePriceCatalog catalog, IReadOnlyList<PipeSegment> pipes)
    {
        string price = ZoneService.PriceFace(face, catalog, pipes, out _);
        ObjectId id = ZoneRenderer.Render(_db, tx, ms, face, price);
        session.Add(id, face);
    }

    private void ReRender(
        Transaction tx, BlockTableRecord ms, ZoneSession.Entry entry,
        PipePriceCatalog catalog, IReadOnlyList<PipeSegment> pipes)
    {
        Erase(tx, entry.Container);
        string price = ZoneService.PriceFace(entry.Face, catalog, pipes, out _);
        entry.Container = ZoneRenderer.Render(_db, tx, ms, entry.Face, price);
    }

    private int RandomColor()
    {
        int r = _rng.Next(60, 225), g = _rng.Next(60, 225), b = _rng.Next(60, 225);
        return (0xFF << 24) | (r << 16) | (g << 8) | b;
    }

    private static void Erase(Transaction tx, ObjectId id)
    {
        if (id.IsErased) return;
        var e = (Entity)tx.GetObject(id, OpenMode.ForWrite);
        e.Erase();
    }

    private static void Erase(Transaction tx, AcPolyline pl)
    {
        pl.UpgradeOpen();
        pl.Erase();
    }
}
