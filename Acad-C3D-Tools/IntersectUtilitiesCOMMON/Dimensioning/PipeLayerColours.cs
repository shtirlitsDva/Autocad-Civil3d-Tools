#if BRICSCAD
using Teigha.Colors;
using Teigha.DatabaseServices;
using Teigha.Geometry;
#else
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
#endif

using IntersectUtilities.UtilsCommon;

using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.Dimensioning
{
    /// <summary>
    /// PIPELAYERSCOLOURSET and PIPELAYERSCOLOURRESET's bodies, the dimensioning
    /// drawing's size colours on the legacy FJV pipe layers, shared by every head
    /// that registers the commands (IntersectUtilities, NorsynDrawingToolsManaged).
    /// No command attribute here: this file compiles into every project that
    /// imports IntersectUtilitiesCOMMON.
    /// </summary>
    public static class PipeLayerColours
    {
        /// <summary>
        /// Sets the color of pipe layers.
        /// Usually used in dimensioning drawings to mark sizes with assigned colors.
        /// </summary>
        public static void Set(Database localDb)
        {
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    List<LayerTableRecord> pipeLtrs = PipeLayers(localDb, tx)
                        .OrderBy(x => GetSystemString(x.Name))
                        .ThenBy(x => GetPipeType(x.Name))
                        .ThenBy(x => GetPipeDN(x.Name))
                        .ToList();
                    prdDbg($"Number of pipe layers in drawing: {pipeLtrs.Count}");

                    //The legend goes at the upper right corner of all pipe polylines.
                    HashSet<string> pipeLayerNames = pipeLtrs.Select(x => x.Name).ToHashSet();
                    List<Extents3d> pipeExtents = localDb.ListOfType<Polyline>(tx)
                        .Where(x => pipeLayerNames.Contains(x.Layer))
                        .Select(x => x.GeometricExtents)
                        .ToList();
                    if (pipeExtents.Count == 0)
                    {
                        prdDbg("No pipe polylines in model space: nothing to colour.");
                        tx.Abort();
                        return;
                    }
                    Point3d upperRightCorner = new Point3d(
                        pipeExtents.Max(x => x.MaxPoint.X),
                        pipeExtents.Max(x => x.MaxPoint.Y),
                        0);

                    //Assign colors and create legend
                    int i = 0;
                    foreach (var layer in pipeLtrs)
                    {
                        Color color = Color.FromColorIndex(ColorMethod.ByAci,
                            GetColorForDim(layer.Name));
                        layer.CheckOrOpenForWrite();
                        layer.Color = color;

                        double vDist = 10;
                        Point3d p1 = new Point3d(upperRightCorner.X, upperRightCorner.Y + vDist * i, 0.0);
                        Point3d p2 = new Point3d(upperRightCorner.X + 50.0, upperRightCorner.Y + vDist * i, 0.0);
                        Line line = new Line(p1, p2);
                        line.AddEntityToDbModelSpace(localDb);
                        line.SetDatabaseDefaults();
                        line.Layer = "0";
                        line.Color = color;
                        DBText text = new DBText();
                        text.AddEntityToDbModelSpace(localDb);
                        text.SetDatabaseDefaults();
                        text.Position = new Point3d(p2.X + 10.0, p2.Y, 0.0);
                        text.Height = 5;
                        text.Color = color;
                        text.TextString =
                            $"{GetSystemString(layer.Name)}{GetPipeDN(layer.Name)}-" +
                            $"{GetPipeType(layer.Name)}";
                        i--;
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        /// <summary>
        /// Resets the color of pipe layers.
        /// Usually used in dimensioning drawings to reset colors.
        /// </summary>
        public static void Reset(Database localDb)
        {
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    List<LayerTableRecord> pipeLtrs = PipeLayers(localDb, tx)
                        .OrderBy(x => x.Name)
                        .ToList();
                    prdDbg($"Number of pipe layers in drawing: {pipeLtrs.Count}");
                    foreach (var ltr in pipeLtrs)
                    {
                        ltr.CheckOrOpenForWrite();
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci,
                            GetLayerColor(GetPipeSystem(ltr.Name), GetPipeType(ltr.Name)));
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        /// <summary>The layers whose name carries a pipe DN.</summary>
        private static IEnumerable<LayerTableRecord> PipeLayers(Database db, Transaction tx)
        {
            LayerTable lt = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (ObjectId ltrid in lt)
            {
                LayerTableRecord ltr = (LayerTableRecord)tx.GetObject(ltrid, OpenMode.ForRead);
                if (GetPipeDN(ltr.Name) != 0) yield return ltr;
            }
        }
    }
}
