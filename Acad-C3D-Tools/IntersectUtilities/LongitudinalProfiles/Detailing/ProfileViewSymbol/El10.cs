using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.ProfileViewSymbol
{
    internal class El10 : ProfileViewSymbol
    {
        public override void CreateSymbol(
            Transaction tx, BlockTableRecord detailingBlock, Point3d location, double dia, string layer)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

            dia = 0.070;

            //Create a round hatch with a diameter of 70 mm
            using (Hatch hatch = new Hatch())
            using (Circle circle = new Circle())
            {
                circle.Center = new Point3d(location.X, location.Y - dia / 2, 0);
                circle.Radius = dia / 2;
                circle.Normal = Vector3d.ZAxis;
                circle.Layer = layer;
                Oid cId = detailingBlock.AppendEntity(circle);
                ObjectIdCollection ids = [cId];

                hatch.Normal = Vector3d.ZAxis;
                hatch.Elevation = 0;
                hatch.PatternScale = 1;
                hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                hatch.AppendLoop(HatchLoopTypes.Outermost, ids);
                hatch.EvaluateHatch(true);

                //tx.AddNewlyCreatedDBObject(circle, true); <- block should do it?
            }
        }
    }
}
