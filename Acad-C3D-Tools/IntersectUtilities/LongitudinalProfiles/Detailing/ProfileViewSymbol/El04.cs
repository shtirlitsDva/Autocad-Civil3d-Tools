using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.ProfileViewSymbol
{
    internal class El04 : BlockBase
    {
        public El04() : base("EL 0.4kV") { }

        public override void CreateDistances(
            BlockTableRecord btr, Matrix3d transform, Point3d labelLocation,
            double dia, string layer, string distance, double kappeOd)
        {
            Transaction tx = btr.Database.TransactionManager.TopTransaction;

            if (!double.TryParse(distance, out double d))
                throw new System.Exception($"Could not parse distance: {distance} to double!");

            foreach (Oid oid in btr)
            {
                if (!oid.IsDerivedFrom<BlockReference>()) continue;
                BlockReference tempBref = oid.Go<BlockReference>(tx);
                //prdDbg("C: " + tempBref.Position.ToString());
                BlockTableRecord tempBtr = tempBref.BlockTableRecord.Go<BlockTableRecord>(tx);
                Point3d theoreticalLocation = new Point3d(labelLocation.X, labelLocation.Y, 0);
                theoreticalLocation = theoreticalLocation.TransformBy(transform);
                //prdDbg("T: " + theoreticalLocation.ToString());
                //prdDbg($"dX: {tempBref.Position.X - theoreticalLocation.X}, dY: {tempBref.Position.Y - theoreticalLocation.Y}");
                if (tempBref.Position.DistanceHorizontalTo(theoreticalLocation) < 0.0001)
                {
                    //prdDbg("Found block!");
                    Extents3d ext = tempBref.GeometricExtents;
                    //prdDbg(ext.ToString());
                    using (Polyline pl = new Polyline(4))
                    {
                        pl.AddVertexAt(0, new Point2d(ext.MinPoint.X, ext.MinPoint.Y), 0.0, 0.0, 0.0);
                        pl.AddVertexAt(1, new Point2d(ext.MinPoint.X, ext.MaxPoint.Y), 0.0, 0.0, 0.0);
                        pl.AddVertexAt(2, new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y), 0.0, 0.0, 0.0);
                        pl.AddVertexAt(3, new Point2d(ext.MaxPoint.X, ext.MinPoint.Y), 0.0, 0.0, 0.0);
                        pl.Closed = true;
                        pl.SetDatabaseDefaults();
                        pl.ReverseCurve();

                        using (DBObjectCollection col = pl.GetOffsetCurves(d))
                        {
                            foreach (var obj in col)
                            {
                                Entity ent = (Entity)obj;
                                ent.Layer = layer;
                                btr.AppendEntity(ent);
                                tx.AddNewlyCreatedDBObject(ent, true);
                            }
                        }
                        using (DBObjectCollection col = pl.GetOffsetCurves(d + kappeOd / 2))
                        {
                            foreach (var obj in col)
                            {
                                Entity ent = (Entity)obj;
                                ent.Layer = layer;
                                btr.AppendEntity(ent);
                                tx.AddNewlyCreatedDBObject(ent, true);
                            }
                        }
                    }
                    break;
                }
            }
        }

        internal override void HandleBlockDefinition(Database localDb)
        {
            localDb.CheckOrImportBlockRecord(
                @"X:\AutoCAD DRI - 01 Civil 3D\Projection_styles.dwg", _blockName);
        }
    }
}
