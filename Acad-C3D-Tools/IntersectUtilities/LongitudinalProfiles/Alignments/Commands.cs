using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.UtilsCommon.DataManager;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        [CommandMethod("CREATEALIGNMENTS")]
        [CommandMethod("CALS")]
        public void createalignments()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataReferencesOptions dro = new();
            if (dro.ProjectName.IsNoE() || dro.EtapeName.IsNoE()) return;

            var dm = new DataManager(dro);
            using Database fjvDb = dm.Fremtid();
            using Transaction fjvTx = fjvDb.TransactionManager.StartTransaction();

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                while (true)
                {
                    List<string> kwds = ["New", "Continue", "Cancel"];
                    var choice = StringGridFormCaller.Call(
                        kwds, "\'New\', \'Continue\' or \'Cancel\':");

                    if (choice == null || choice == "Cancel") break;


                }
            }
            catch (System.Exception ex)
            {
                fjvTx.Abort();
                tx.Abort();
                prdDbg(ex);
                return;
            }

            fjvTx.Commit();
            tx.Commit();
        }
    }
}
