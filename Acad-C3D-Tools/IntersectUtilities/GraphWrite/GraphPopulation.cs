using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.GraphWrite
{
    public static partial class GraphPopulation
    {
        /// <summary>
        /// Populates the drawing's DriGraph through AEC, in its own committed
        /// transaction. Throws when it fails, and then writes nothing.
        /// </summary>
        public static void Populate(Database localDb)
        {
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Populate(localDb, tx, new PropertySetHelper(localDb));
                }
                catch
                {
                    tx.Abort();
                    throw;
                }
                tx.Commit();
            }
        }

        /// <summary>
        /// Clears the drawing's DriGraph through AEC, in its own committed
        /// transaction. Throws when it fails, and then writes nothing.
        /// </summary>
        public static void Clear(Database localDb)
        {
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Clear(localDb, tx, new PropertySetHelper(localDb));
                }
                catch
                {
                    tx.Abort();
                    throw;
                }
                tx.Commit();
            }
        }
    }
}
