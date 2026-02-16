using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

namespace SheetCreationAutomation.Procedures.ViewFrames
{
    internal sealed class ViewFrameCountService
    {
        public int GetViewFrameCount(Database database)
        {
            using Transaction tx = database.TransactionManager.StartTransaction();
            BlockTable blockTable = (BlockTable)tx.GetObject(database.BlockTableId, OpenMode.ForRead);
            int count = 0;

            foreach (ObjectId btrId in blockTable)
            {
                BlockTableRecord btr = (BlockTableRecord)tx.GetObject(btrId, OpenMode.ForRead);
                foreach (ObjectId entityId in btr)
                {
                    Autodesk.AutoCAD.DatabaseServices.DBObject obj =
                        tx.GetObject(entityId, OpenMode.ForRead, false);
                    if (obj is ViewFrame)
                    {
                        count++;
                    }
                }
            }

            tx.Abort();
            return count;
        }
    }
}
