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

using System.Collections.Generic;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.LER2
{
    /// <summary>
    /// LER2SPLIT's body, shared by every head that registers the command
    /// (IntersectUtilities on Civil 3D, NorsynDrawingToolsManaged on BricsCAD). No command
    /// attribute here: this file compiles into every project that imports
    /// IntersectUtilitiesCOMMON. The host's property sets come in through the copier.
    /// </summary>
    public static class Ler2Split
    {
        /// <summary>
        /// Asks for a polyline or 3D polyline, then for a point on it, and splits it there
        /// into two, each with the source's layer, look and property sets. The source is
        /// replaced by the two pieces in one undoable change.
        /// </summary>
        /// <param name="doc">The drawing the drafter works in.</param>
        /// <param name="sets">The host's property set copier.</param>
        public static void Split(Document doc, IPropertySetCopier sets)
        {
            Database db = doc.Database;
            Editor ed = doc.Editor;

            var opt = new PromptEntityOptions("\nSelect the polyline or 3D polyline to split: ");
            opt.SetRejectMessage("\nSelect a polyline or 3D polyline!");
            opt.AddAllowedClass(typeof(Polyline), true);
            opt.AddAllowedClass(typeof(Polyline3d), true);
            PromptEntityResult chosen = ed.GetEntity(opt);
            if (chosen.Status != PromptStatus.OK) return;

            PromptPointResult picked = ed.GetPoint("\nPick the point to split it at: ");
            if (picked.Status != PromptStatus.OK) return;
            Point3d at = picked.Value.TransformBy(ed.CurrentUserCoordinateSystem);

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var source = (Curve)tx.GetObject(chosen.ObjectId, OpenMode.ForRead);
                    if (source.Closed)
                    {
                        prdDbg($"Polyline {source.Handle} is closed: not split.");
                        return;
                    }

                    // In plan: the curve is split at its point under the pick, so a 3D
                    // polyline's new vertex gets the elevation of the segment there.
                    Point3d onCurve = source.GetClosestPointTo(at, Vector3d.ZAxis, false);
                    if (onCurve.IsEqualTo(source.StartPoint) || onCurve.IsEqualTo(source.EndPoint))
                    {
                        prdDbg($"The point is at an end of polyline {source.Handle}: nothing to split.");
                        return;
                    }

                    DBObjectCollection pieces = source.GetSplitCurves(new Point3dCollection { onCurve });
                    if (pieces.Count != 2)
                    {
                        foreach (DBObject piece in pieces) piece.Dispose();
                        prdDbg($"Polyline {source.Handle} did not split in two at that point: not split.");
                        return;
                    }

                    var owner = (BlockTableRecord)tx.GetObject(source.OwnerId, OpenMode.ForWrite);
                    var handles = new List<string>();
                    foreach (Entity piece in pieces)
                    {
                        piece.SetPropertiesFrom(source);
                        owner.AppendEntity(piece);
                        tx.AddNewlyCreatedDBObject(piece, true);
                        sets.CopyAll(source, piece);
                        handles.Add(piece.Handle.ToString());
                    }

                    // Last: the copier reads the source's sets.
                    string sourceHandle = source.Handle.ToString();
                    source.UpgradeOpen();
                    source.Erase();
                    tx.Commit();
                    prdDbg($"Polyline {sourceHandle} split into {handles[0]} and {handles[1]}.");
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                }
            }
        }
    }
}
