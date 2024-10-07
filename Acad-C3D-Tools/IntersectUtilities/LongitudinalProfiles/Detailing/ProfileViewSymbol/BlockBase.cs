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
    }
}
