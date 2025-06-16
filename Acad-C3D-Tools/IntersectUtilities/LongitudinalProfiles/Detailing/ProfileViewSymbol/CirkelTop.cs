using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Distance;
using static IntersectUtilities.UtilsCommon.Utils;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.ProfileViewSymbol
{
    internal class CirkelTop : ProfileViewSymbol
    {
        public override void CreateDistances(
            BlockTableRecord btr,
            Matrix3d transform,
            Point3d labelLocation,
            double dia,
            string layer,
            string distance,
            double kappeOd,
            bool isRelocatable
        )
        {
            Transaction tx = btr.Database.TransactionManager.TopTransaction;

            var dcd = new PSetDefs.DriCrossingData();
            PropertySetManager psm = new PropertySetManager(btr.Database, dcd.SetName);

            if (!double.TryParse(distance, out double d))
                throw new System.Exception($"Could not parse distance: {distance} to double!");

            Circle? circle = null;
            foreach (Oid oid in btr)
            {
                if (!oid.IsDerivedFrom<Circle>())
                    continue;
                Circle tempC = oid.Go<Circle>(tx);
                Point3d theoreticalLocation = new Point3d(
                    labelLocation.X,
                    labelLocation.Y - (dia / 2),
                    0
                );
                theoreticalLocation = theoreticalLocation.TransformBy(transform);
                if (tempC.Center.DistanceHorizontalTo(theoreticalLocation) < 0.0001)
                {
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
                        tx.AddNewlyCreatedDBObject(ent, true);

                        psm.WritePropertyObject(ent, dcd.CanBeRelocated, isRelocatable);
                    }
                }
                using (DBObjectCollection col = circle.GetOffsetCurves(d + kappeOd / 2))
                {
                    foreach (var obj in col)
                    {
                        Entity ent = (Entity)obj;
                        ent.Layer = layer;
                        btr.AppendEntity(ent);
                        tx.AddNewlyCreatedDBObject(ent, true);

                        psm.WritePropertyObject(ent, dcd.CanBeRelocated, isRelocatable);
                    }
                }
            }
        }

        public override void CreateSymbol(
            BlockTable bt,
            BlockTableRecord detailingBlock,
            Point3d location,
            double dia,
            string layer,
            bool isRelocatable
        )
        {
            using Circle circle = new Circle();

            circle.Center = new Point3d(location.X, location.Y - (dia / 2), 0);
            circle.Radius = dia / 2;
            circle.Normal = Vector3d.ZAxis;
            circle.Layer = layer;

            detailingBlock.AppendEntity(circle);
            bt.Database.TransactionManager.TopTransaction.AddNewlyCreatedDBObject(circle, true);

            PropertySetManager psm = new PropertySetManager(
                bt.Database,
                PSetDefs.DefinedSets.DriCrossingData
            );

            psm.WritePropertyObject(
                circle,
                new PSetDefs.DriCrossingData().CanBeRelocated,
                isRelocatable
            );
        }
    }
}
