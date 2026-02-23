using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using DimensioneringV2.BBRData.Models;

using IntersectUtilities;
using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.UtilsCommon.Utils;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using cv = DimensioneringV2.CommonVariables;

namespace DimensioneringV2.BBRData.AutoCAD
{
    internal static class BbrBlockReader
    {
        /// <summary>
        /// Reads all BBR blocks from the current drawing and snapshots their property values.
        /// Must be called from a context where AutoCAD document access is available.
        /// </summary>
        public static List<BbrRowData> ReadAll()
        {
            var results = new List<BbrRowData>();
            var allProps = BbrPropertyDescriptor.All;

            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            using DocumentLock docLock = docCol.MdiActiveDocument.LockDocument();
            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                var brs = localDb.ListOfType<BlockReference>(tx, true)
                    .Where(x => cv.AllBlockTypes.Contains(
                        PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")));

                foreach (var br in brs)
                {
                    try
                    {
                        var bbr = new BBR(br);
                        var values = new Dictionary<string, object?>();

                        foreach (var prop in allProps)
                        {
                            try
                            {
                                values[prop.Name] = prop.GetValue(bbr);
                            }
                            catch
                            {
                                values[prop.Name] = null;
                            }
                        }

                        results.Add(new BbrRowData(br.ObjectId, values));
                    }
                    catch
                    {
                        // Skip blocks that fail to create BBR wrapper
                    }
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
            }

            return results;
        }
    }
}
