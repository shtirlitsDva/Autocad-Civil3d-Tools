using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Distance;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.ProfileViewSymbol
{
    internal class CirkelTop : ProfileViewSymbol
    {
        public override void CreateDistances(
            BlockTableRecord btr, Matrix3d transform, Point3d labelLocation,
            double dia, string layer, string distance, double kappeOd)
        {
            Transaction tx = btr.Database.TransactionManager.TopTransaction;

            if (!double.TryParse(distance, out double d))
                throw new System.Exception($"Could not parse distance: {distance} to double!");

            Circle circle = null;

            foreach (Oid oid in btr)
            {
                if (!oid.IsDerivedFrom<Circle>()) continue;
                Circle tempC = oid.Go<Circle>(tx);
                Point3d theoreticalLocation = new Point3d(labelLocation.X, labelLocation.Y - (dia / 2), 0);
                theoreticalLocation = theoreticalLocation.TransformBy(transform);
                if (tempC.Center.DistanceHorizontalTo(theoreticalLocation) < 0.0001)
                {
#if DEBUG
                    prdDbg("Found Cirkel, Top!");
#endif
                    circle = tempC;
                    break;
                }
            }

            if (circle != null)
            {
                using (DBObjectCollection col = circle.GetOffsetCurves(d))
                {
                    foreach (var obj in col)
                    {
                        Entity ent = (Entity)obj;
                        ent.Layer = layer;
                        btr.AppendEntity(ent);
                        //tx.AddNewlyCreatedDBObject(ent, true);
                    }
                }
                using (DBObjectCollection col = circle.GetOffsetCurves(d + kappeOd / 2))
                {
                    foreach (var obj in col)
                    {
                        Entity ent = (Entity)obj;
                        ent.Layer = layer;
                        btr.AppendEntity(ent);
                        //tx.AddNewlyCreatedDBObject(ent, true);
                    }
                }
            }
        }

        public override void CreateSymbol(
            BlockTable bt, BlockTableRecord detailingBlock, Point3d location,
            double dia, string layer)
        {
            using (Circle circle = new Circle())
            {
                circle.Center = new Point3d(location.X, location.Y - (dia / 2), 0);
                circle.Radius = dia / 2;
                circle.Normal = Vector3d.ZAxis;
                circle.Layer = layer;

                detailingBlock.AppendEntity(circle);
                //tx.AddNewlyCreatedDBObject(circle, true); <- block should do it?
            }
        }
    }
}
