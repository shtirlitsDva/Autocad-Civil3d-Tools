using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

using static IntersectUtilities.UtilsCommon.Utils;
using IntersectUtilities.UtilsCommon;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.ProfileViewSymbol
{
    internal abstract class BlockBase : ProfileViewSymbol
    {
        protected string _blockName { get; set; }
        protected BlockBase(string blockName) { _blockName = blockName; }
        public override void CreateSymbol(
            BlockTable bt, BlockTableRecord detailingBlock, Point3d location,
            double dia, string layer)
        {
            var br = new BlockReference(location, bt[_blockName]);
            br.Layer = layer;
            detailingBlock.AppendEntity(br);
        }
        internal abstract void HandleBlockDefinition(Database localDb);
        protected void CreateBlockTableRecord(Database localDb, double dia)
        {
            Transaction tx = localDb.TransactionManager.TopTransaction;
            BlockTable bt = (BlockTable)tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite);
            if (!bt.Has(_blockName))
            {
                BlockTableRecord mspace = (BlockTableRecord)tx
                    .GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                if (!bt.Has(_blockName))
                {
                    using (BlockTableRecord symbolBtr = new BlockTableRecord())
                    {
                        symbolBtr.Name = _blockName;
                        symbolBtr.Origin = new Point3d(0, 0, 0);

                        using (Hatch hatch = new Hatch())
                        using (Circle circle = new Circle())
                        {
                            circle.Center = new Point3d(0, 0 - dia / 2, 0);
                            circle.Radius = dia / 2;
                            circle.Normal = Vector3d.ZAxis;
                            Oid cId = mspace.AppendEntity(circle);
                            tx.AddNewlyCreatedDBObject(circle, true);
                            ObjectIdCollection ids = [cId];

                            hatch.Normal = Vector3d.ZAxis;
                            hatch.Elevation = 0;
                            hatch.PatternScale = 1;
                            hatch.Color = UtilsCommon.Utils.ColorByName("red");
                            hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                            hatch.AppendLoop(HatchLoopTypes.Outermost, ids);
                            hatch.EvaluateHatch(true);

                            symbolBtr.AppendEntity(hatch);

                            tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite);
                            bt.Add(symbolBtr);
                            tx.AddNewlyCreatedDBObject(symbolBtr, true);

                            circle.UpgradeOpen();
                            circle.Erase(true);
                        }
                    }
                }
            }
        }
        public override void CreateDistances(
            BlockTableRecord btr, Matrix3d transform, Point3d labelLocation,
            double dia, string layer, string distance, double kappeOd)
        {
            Transaction tx = btr.Database.TransactionManager.TopTransaction;

            //this because I haven't completely figured out neat inheritance yet
            dia = getDia();

            double topDist;
            double botDist;
            if (distance.Contains("|"))
            {
                var split = distance.Split('|');
                if (!double.TryParse(split[0], out topDist))
                    throw new System.Exception($"Could not parse distance: {split[0]} to double!");
                if (!double.TryParse(split[1], out botDist))
                    throw new System.Exception($"Could not parse distance: {split[1]} to double!");
            }
            else throw new System.Exception($"Distance definition: {distance} does not follow convention!");

            Point3d theoreticalLocation = new Point3d(labelLocation.X, labelLocation.Y - (dia / 2), 0);
            theoreticalLocation = theoreticalLocation.TransformBy(transform);

            Arc a;
            Arc createArc(Point3d c, double r, double s, double e)
            {
                Arc arc = new Arc();
                arc.Center = c;
                arc.Radius = r;
                arc.StartAngle = s;
                arc.EndAngle = e;
                arc.Normal = Vector3d.ZAxis;
                arc.Layer = layer;
                a = arc;
                return arc;
            }
            
            btr.AppendEntity(createArc(theoreticalLocation, dia / 2 + botDist, Math.PI, 2 * Math.PI));
            tx.AddNewlyCreatedDBObject(a, true);
            btr.AppendEntity(createArc(theoreticalLocation, dia / 2 + botDist + kappeOd / 2, Math.PI, 2 * Math.PI));
            tx.AddNewlyCreatedDBObject(a, true);
            btr.AppendEntity(createArc(theoreticalLocation, dia / 2 + topDist, 0, Math.PI));
            tx.AddNewlyCreatedDBObject(a, true);
            btr.AppendEntity(createArc(theoreticalLocation, dia / 2 + topDist + kappeOd / 2, 0, Math.PI));
            tx.AddNewlyCreatedDBObject(a, true);

        }
        protected virtual double getDia() => 0;
    }
}
