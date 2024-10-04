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
        public override void CreateSymbol(
            Transaction tx, BlockTableRecord detailingBlock, Point3d location, double dia, string layer)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx2 = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTable bt = tx2.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;
                    BlockTableRecord mspace = tx2.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    dia = 0.070;

                    if (!bt.Has(_blockName))
                    {
                        //Create a round hatch with a diameter of 70 mm
                        using (BlockTableRecord symbol = new BlockTableRecord())
                        using (Hatch hatch = new Hatch())
                        using (Circle circle = new Circle())
                        {
                            circle.Center = new Point3d(location.X, location.Y - dia / 2, 0);
                            circle.Radius = dia / 2;
                            circle.Normal = Vector3d.ZAxis;
                            circle.Layer = layer;
                            Oid cId = mspace.AppendEntity(circle);
                            tx2.AddNewlyCreatedDBObject(circle, true);
                            ObjectIdCollection ids = [cId];

                            hatch.Normal = Vector3d.ZAxis;
                            hatch.Elevation = 0;
                            hatch.PatternScale = 1;
                            hatch.Layer = layer;
                            hatch.Color = UtilsCommon.Utils.ColorByName("red");
                            hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                            hatch.AppendLoop(HatchLoopTypes.Outermost, ids);
                            hatch.EvaluateHatch(true);

                            symbol.Origin = location;
                            symbol.Name = _blockName;
                            symbol.AppendEntity(hatch);
                            bt.Add(symbol);
                        }
                    }

                    using (var br = new BlockReference(location, bt[_blockName]))
                    {
                        br.Layer = layer;
                        detailingBlock.AppendEntity(br);
                    }

                    tx2.Commit();
                }
                catch (Exception ex)
                {
                    prdDbg(ex);
                    tx2.Abort();
                    throw;
                }
            }
        }
    }
}
