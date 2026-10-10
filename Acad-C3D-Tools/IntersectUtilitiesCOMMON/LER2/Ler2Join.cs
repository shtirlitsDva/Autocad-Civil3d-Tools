#if BRICSCAD
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Teigha.DatabaseServices;
using Teigha.Geometry;
#else
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
#endif

using IntersectUtilities.UtilsCommon;

using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.LER2
{
    /// <summary>
    /// LER2JOIN's body, shared by every head that registers the command
    /// (IntersectUtilities, NorsynDrawingToolsManaged). No command attribute here: this
    /// file compiles into every project that imports IntersectUtilitiesCOMMON.
    /// </summary>
    public static class Ler2Join
    {
        /// <summary>How close two ends must lie in plan to be one point.</summary>
        private const double Tolerance = 0.001;

        /// <summary>
        /// Asks for two 3D polylines that meet end to end, then for the one whose property
        /// sets to keep, and joins them into that one. The kept polyline keeps its identity,
        /// layer, property sets and direction and takes the other's vertices; the other is
        /// erased. At the joint the kept polyline's vertex stays, unless it is at -99 and the
        /// other's has an elevation.
        /// </summary>
        /// <param name="doc">The drawing the drafter works in.</param>
        public static void Join(Document doc)
        {
            Database db = doc.Database;
            Editor ed = doc.Editor;

            ObjectId first = Pick(ed, "\nSelect the first 3D polyline to join: ");
            if (first.IsNull) return;
            ObjectId second = Pick(ed, "\nSelect the second 3D polyline to join: ");
            if (second.IsNull) return;
            if (second == first)
            {
                prdDbg("That is the same 3D polyline twice: not joined.");
                return;
            }

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var a = (Polyline3d)tx.GetObject(first, OpenMode.ForRead);
                    var b = (Polyline3d)tx.GetObject(second, OpenMode.ForRead);
                    foreach (Polyline3d pl in new[] { a, b })
                    {
                        if (pl.Closed || pl.PolyType != Poly3dType.SimplePoly)
                        {
                            prdDbg($"3D polyline {pl.Handle} is closed or curve-fitted: not joined.");
                            return;
                        }
                    }

                    Point3d[] pa = a.GetVertices(tx).Select(v => v.Position).ToArray();
                    Point3d[] pb = b.GetVertices(tx).Select(v => v.Position).ToArray();
                    var meets = new List<(bool AtEndOfA, bool AtEndOfB)>();
                    foreach (bool endA in new[] { false, true })
                        foreach (bool endB in new[] { false, true })
                            if (End(pa, endA).HorizontalEqualz(End(pb, endB), Tolerance)) meets.Add((endA, endB));
                    if (meets.Count == 0)
                    {
                        prdDbg($"3D polylines {a.Handle} and {b.Handle} have no ends in common: not joined.");
                        return;
                    }
                    if (meets.Count > 1)
                    {
                        prdDbg($"3D polylines {a.Handle} and {b.Handle} meet at both ends: joining would close a loop. Not joined.");
                        return;
                    }

                    ObjectId keptId = ChooseKept(ed, first, second);
                    if (keptId.IsNull) return;
                    bool keepA = keptId == first;
                    Polyline3d kept = keepA ? a : b;
                    Polyline3d other = keepA ? b : a;
                    Point3d[] pk = keepA ? pa : pb;
                    Point3d[] po = keepA ? pb : pa;
                    bool keptAtEnd = keepA ? meets[0].AtEndOfA : meets[0].AtEndOfB;
                    bool otherAtEnd = keepA ? meets[0].AtEndOfB : meets[0].AtEndOfA;

                    Point3d joint = Joint(End(pk, keptAtEnd), End(po, otherAtEnd));
                    // The kept polyline runs on in its own direction: the other is laid
                    // after its end, or before its start.
                    IEnumerable<Point3d> otherAway = otherAtEnd ? Enumerable.Reverse(po) : po;
                    List<Point3d> joined = keptAtEnd
                        ? pk.Take(pk.Length - 1).Append(joint).Concat(otherAway.Skip(1)).ToList()
                        : otherAway.Reverse().Take(po.Length - 1).Append(joint).Concat(pk.Skip(1)).ToList();

                    kept.UpgradeOpen();
                    PolylineVertex3d[] vertices = kept.GetVertices(tx);
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        vertices[i].CheckOrOpenForWrite();
                        vertices[i].Position = joined[i];
                    }
                    for (int i = vertices.Length; i < joined.Count; i++)
                    {
                        var vertex = new PolylineVertex3d(joined[i]);
                        kept.AppendVertex(vertex);
                        tx.AddNewlyCreatedDBObject(vertex, true);
                    }

                    string otherHandle = other.Handle.ToString();
                    other.UpgradeOpen();
                    other.Erase();
                    tx.Commit();
                    prdDbg($"3D polyline {otherHandle} joined into {kept.Handle}, which keeps its property sets.");
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                }
            }
        }

        private static ObjectId Pick(Editor ed, string message)
        {
            var opt = new PromptEntityOptions(message);
            opt.SetRejectMessage("\nSelect a 3D polyline!");
            opt.AddAllowedClass(typeof(Polyline3d), true);
            PromptEntityResult res = ed.GetEntity(opt);
            return res.Status == PromptStatus.OK ? res.ObjectId : ObjectId.Null;
        }

        /// <summary>
        /// The one of the two whose property sets the drafter keeps, asked with both
        /// highlighted; ObjectId.Null on cancel.
        /// </summary>
        private static ObjectId ChooseKept(Editor ed, ObjectId first, ObjectId second)
        {
            Highlight(first, second, on: true);
            try
            {
                while (true)
                {
                    ObjectId picked = Pick(ed, "\nSelect the 3D polyline whose property sets to keep: ");
                    if (picked.IsNull || picked == first || picked == second) return picked;
                    prdDbg("Select one of the two 3D polylines being joined.");
                }
            }
            finally
            {
                Highlight(first, second, on: false);
            }
        }

        private static void Highlight(ObjectId first, ObjectId second, bool on)
        {
            using (Transaction tx = first.Database.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (ObjectId id in new[] { first, second })
                {
                    var ent = (Entity)tx.GetObject(id, OpenMode.ForRead);
                    if (on) ent.Highlight();
                    else ent.Unhighlight();
                }
                tx.Commit();
            }
        }

        private static Point3d End(Point3d[] points, bool atEnd) => atEnd ? points[points.Length - 1] : points[0];

        /// <summary>
        /// The joint vertex: the kept polyline's, unless it is at -99 (or 0) and the other's
        /// has an elevation. Two different elevations are reported.
        /// </summary>
        private static Point3d Joint(Point3d kept, Point3d other)
        {
            if (kept.Z.is2D() && other.Z.is3D()) return new Point3d(kept.X, kept.Y, other.Z);
            if (kept.Z.is3D() && other.Z.is3D() && System.Math.Abs(kept.Z - other.Z) > Tolerance)
                prdDbg($"The two ends lie at {kept.Z:0.###} and {other.Z:0.###}: the joint keeps {kept.Z:0.###}.");
            return kept;
        }
    }
}
