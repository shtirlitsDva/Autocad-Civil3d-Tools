using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.ProfileViewSymbol
{
    internal class El04 : ProfileViewSymbol
    {
        private string _blockName;
        public El04(string blockName) { _blockName = blockName; }
        public override void CreateSymbol(Transaction tx, BlockTableRecord detailingBlock, Point3d location, double dia, string layer)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

            if (!bt.Has(_blockName))
            {
                new IntersectUtilities.Intersect().importcivilstyles();
            }

            using (var br = new BlockReference(location, bt[_blockName]))
            {
                br.Layer = layer;
                detailingBlock.AppendEntity(br);
            }
        }
    }
}
