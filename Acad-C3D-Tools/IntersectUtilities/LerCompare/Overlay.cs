using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace IntersectUtilities.LerCompare;

internal sealed class Overlay
{
    private readonly List<Entity> entities = new();
    private readonly IntegerCollection viewports = new();
    public int Count => entities.Count;
    public void Clear()
    {
        foreach (var e in entities)
        {
            try
            {
                TransientManager.CurrentTransientManager.EraseTransient(e, viewports);
            }
            catch (System.Exception error) { UtilsCommon.Utils.prdDbg("LER Compare overlay cleanup: " + error.Message); }
            finally { e.Dispose(); }
        }
        entities.Clear();
    }
    private void Add(Entity e)
    {
        bool added = false;
        try
        {
            added = TransientManager.CurrentTransientManager.AddTransient(e, TransientDrawingMode.DirectTopmost, 128, viewports);
            if (added)
                entities.Add(e);
        }
        finally { if (!added) e.Dispose(); }
    }
    public static short Color(Change flags)
    {
        if (flags.HasFlag(Change.Review))
            return 6;
        if (flags.HasFlag(Change.Coverage))
            return 30;
        if (flags.HasFlag(Change.New))
            return 3;
        if (flags.HasFlag(Change.Missing))
            return 1;
        if (flags.HasFlag(Change.Status))
            return 2;
        if (flags.HasFlag(Change.Material))
            return 4;
        if (flags.HasFlag(Change.Geometry))
            return 5;
        if (flags.HasFlag(Change.Split) || flags.HasFlag(Change.Merged))
            return 140;
        return 7;
    }
    private static Polyline Make(Pipe pipe, short color)
    {
        var p = new Polyline();
        for (int i = 0; i < pipe.Points.Count; i++)
            p.AddVertexAt(i, new Point2d(pipe.Points[i].X, pipe.Points[i].Y), i < pipe.Bulges.Count ? pipe.Bulges[i] : 0, 0, 0);
        p.Closed = pipe.Closed;
        p.ColorIndex = color;
        p.LineWeight = LineWeight.LineWeight050;
        return p;
    }
    public void Draw(IEnumerable<Result> rows, bool old, bool current, bool vertices, double tolerance)
    {
        Clear();
        var drawn = new HashSet<(Pipe, bool)>();
        foreach (var r in rows)
        {
            short color = Color(r.Flags);
            if (old)
                foreach (var p in r.Old.Concat(r.CandidateOld))
                    if (drawn.Add((p, false)))
                        Add(Make(p, r.CandidateOld.Contains(p) || r.New.Count > 0 ? (short)8 : color));
            if (current)
                foreach (var p in r.New.Concat(r.CandidateNew))
                    if (drawn.Add((p, true)))
                        Add(Make(p, r.CandidateNew.Contains(p) ? (short)8 : color));
            if (vertices && r.Flags.HasFlag(Change.Geometry) && r.Old.Count == 1 && r.New.Count == 1)
            {
                if (old)
                    Mark(r.Old[0], r.OldChangedVertices, r.OldChangedEdges, 1, tolerance);
                if (current)
                    Mark(r.New[0], r.NewChangedVertices, r.NewChangedEdges, 4, tolerance);
            }
        }
        AcadApp.UpdateScreen();
    }
    private void Mark(Pipe p, List<int> vertexIds, List<int> edgeIds, short color, double tolerance)
    {
        foreach (int i in edgeIds)
        {
            int j = (i + 1) % p.Points.Count;
            var edge = new Pipe { Points = new() { p.Points[i], p.Points[j] }, Bulges = new() { i < p.Bulges.Count ? p.Bulges[i] : 0, 0 } };
            var entity = Make(edge, color);
            entity.LineWeight = LineWeight.LineWeight080;
            Add(entity);
        }
        foreach (int i in vertexIds)
            Add(new Circle(new Point3d(p.Points[i].X, p.Points[i].Y, 0), Vector3d.ZAxis, Math.Max(tolerance * 5, tolerance / .01 * .12)) { ColorIndex = color, LineWeight = LineWeight.LineWeight050 });
    }
}
