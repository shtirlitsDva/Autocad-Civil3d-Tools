using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using NetTopologySuite.Geometries;

using NorsynDistrictZones.Acad;
using NorsynDistrictZones.Model;
using NorsynDistrictZones.Pricing;
using NorsynDistrictZones.Topology;

using NsContainer = NorsynObjectsInterop.NorsynContainer;

namespace NorsynDistrictZones.Editing;

/// <summary>One draggable grip = one boundary vertex of a zone face.</summary>
internal sealed class ZoneVertexGrip : GripData
{
    public ZoneVertexGrip(Point3d point) { GripPoint = point; }
}

/// <summary>
/// Exposes each zone face's boundary vertices as grips. Moving a vertex moves it in
/// the gripped face AND in every other face that shares that exact coordinate — so
/// adjacent zones adapt together (shared-edge behaviour, matched by coordinate). All
/// affected faces are rebuilt in place (same ObjectIds) and re-priced.
/// </summary>
internal sealed class ZoneGripOverrule : GripOverrule
{
    private const double MatchTol = 1e-6;
    private static ZoneGripOverrule? _instance;

    public static void Enable()
    {
        if (_instance is not null) return;
        _instance = new ZoneGripOverrule();
        AddOverrule(RXObject.GetClass(typeof(NsContainer)), _instance, false);
        Overrule.Overruling = true;
    }

    public static void Disable()
    {
        if (_instance is null) return;
        try { RemoveOverrule(RXObject.GetClass(typeof(NsContainer)), _instance); } catch { }
        _instance.Dispose();
        _instance = null;
    }

    public override bool IsApplicable(RXObject overruledSubject) =>
        overruledSubject is Entity e && IsZone(e);

    private static bool IsZone(Entity e)
    {
        try { return e.GetRXClass().Name == "NorsynContainer"; } catch { return false; }
    }

    public override void GetGripPoints(
        Entity entity, GripDataCollection grips, double curViewUnitSize, int gripSize,
        Vector3d curViewDir, GetGripPointsFlags bitFlags)
    {
        ZoneFace? face = ZoneXData.ReadFace(entity);
        if (face is null) { base.GetGripPoints(entity, grips, curViewUnitSize, gripSize, curViewDir, bitFlags); return; }

        Coordinate[] ring = face.Polygon.ExteriorRing.Coordinates;
        int n = ring.Length > 1 && ring[0].Equals2D(ring[^1]) ? ring.Length - 1 : ring.Length;
        for (int i = 0; i < n; i++)
            grips.Add(new ZoneVertexGrip(new Point3d(ring[i].X, ring[i].Y, 0)));
    }

    public override void MoveGripPointsAt(
        Entity entity, GripDataCollection grips, Vector3d offset, MoveGripPointsFlags bitFlags)
    {
        var moves = new List<(Point3d Old, Point3d New)>();
        foreach (GripData g in grips)
            if (g is ZoneVertexGrip zg)
                moves.Add((zg.GripPoint, zg.GripPoint + offset));

        if (moves.Count == 0)
        {
            base.MoveGripPointsAt(entity, grips, offset, bitFlags);
            return;
        }

        try { Apply(entity, moves); }
        catch (System.Exception ex)
        {
            entity.Database?.TransactionManager?.QueueForGraphicsFlush();
            Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                .MdiActiveDocument?.Editor?.WriteMessage($"\nNDZ grip edit failed: {ex.Message}\n");
        }
    }

    /// <summary>
    /// Apply vertex moves across EVERY container by coordinate match (no pre-opened
    /// gripped entity). Used by the dev harness to validate shared-vertex adjacency.
    /// Returns the number of faces changed.
    /// </summary>
    public static int ApplyMovesAllContainers(Database db, List<(Point3d Old, Point3d New)> moves)
    {
        using Transaction tx = db.TransactionManager.StartTransaction();
        IReadOnlyList<PipeSegment> pipes = PipeReader.ReadFromXrefs(db, tx, null);
        PipePriceCatalog catalog = CatalogStore.GetActive(db);
        var ms = (BlockTableRecord)tx.GetObject(
            SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);

        int changed = 0;
        foreach (ObjectId id in ms)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not Entity e || !IsZone(e)) continue;
            ZoneFace? face = ZoneXData.ReadFace(e);
            if (face is null || !Touches(face.Polygon, moves)) continue;
            var nc = (NsContainer)tx.GetObject(id, OpenMode.ForWrite);
            UpdateContainer(db, tx, nc, face, moves, pipes, catalog);
            changed++;
        }
        tx.Commit();
        Reactors.ZoneSession.For(db).Clear();
        return changed;
    }

    private static void Apply(Entity grippedEntity, List<(Point3d Old, Point3d New)> moves)
    {
        Database db = grippedEntity.Database;
        using Transaction tx = db.TransactionManager.StartTransaction();

        IReadOnlyList<PipeSegment> pipes = PipeReader.ReadFromXrefs(db, tx, null);
        PipePriceCatalog catalog = CatalogStore.GetActive(db);

        // The gripped container is already open for write (by the grip framework) —
        // modify it directly, never re-GetObject its id (that would conflict).
        UpdateContainer(db, tx, (NsContainer)grippedEntity, ZoneXData.ReadFace(grippedEntity), moves, pipes, catalog);

        // Neighbours sharing any moved vertex follow.
        var ms = (BlockTableRecord)tx.GetObject(
            SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
        foreach (ObjectId id in ms)
        {
            if (id == grippedEntity.ObjectId) continue;
            if (tx.GetObject(id, OpenMode.ForRead) is not Entity e || !IsZone(e)) continue;
            ZoneFace? face = ZoneXData.ReadFace(e);
            if (face is null) continue;
            if (!Touches(face.Polygon, moves)) continue;

            var nc = (NsContainer)tx.GetObject(id, OpenMode.ForWrite);
            UpdateContainer(db, tx, nc, face, moves, pipes, catalog);
        }

        tx.Commit();
        Reactors.ZoneSession.For(db).Clear(); // force the reactor to reload from the new geometry
    }

    private static void UpdateContainer(
        Database db, Transaction tx, NsContainer nc, ZoneFace? face,
        List<(Point3d Old, Point3d New)> moves, IReadOnlyList<PipeSegment> pipes, PipePriceCatalog catalog)
    {
        if (face is null) return;
        bool changed = false;
        Polygon moved = MoveVertices(face.Polygon, moves, ref changed);
        if (!changed) return;
        face.Polygon = moved;
        string price = ZoneService.PriceFace(face, catalog, pipes, out _);
        ZoneRenderer.Update(db, tx, nc, face, price);
    }

    private static bool Touches(Polygon poly, List<(Point3d Old, Point3d New)> moves)
    {
        foreach (Coordinate c in poly.Coordinates)
            foreach (var (o, _) in moves)
                if (Math.Abs(c.X - o.X) < MatchTol && Math.Abs(c.Y - o.Y) < MatchTol) return true;
        return false;
    }

    private static Polygon MoveVertices(Polygon poly, List<(Point3d Old, Point3d New)> moves, ref bool changed)
    {
        GeometryFactory gf = poly.Factory;
        bool any = false;

        Coordinate Map(Coordinate c)
        {
            foreach (var (o, n) in moves)
                if (Math.Abs(c.X - o.X) < MatchTol && Math.Abs(c.Y - o.Y) < MatchTol)
                {
                    any = true;
                    return new Coordinate(n.X, n.Y);
                }
            return c.Copy();
        }

        LinearRing shell = gf.CreateLinearRing(poly.ExteriorRing.Coordinates.Select(Map).ToArray());
        var holes = new LinearRing[poly.NumInteriorRings];
        for (int i = 0; i < poly.NumInteriorRings; i++)
            holes[i] = gf.CreateLinearRing(poly.GetInteriorRingN(i).Coordinates.Select(Map).ToArray());

        changed = any;
        return gf.CreatePolygon(shell, holes);
    }
}
