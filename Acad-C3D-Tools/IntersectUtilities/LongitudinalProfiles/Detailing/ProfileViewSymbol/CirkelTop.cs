using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NetTopologySuite.Geometries;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.ProfileViewSymbol
{
    internal class CirkelTop : ProfileViewSymbol
    {
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
