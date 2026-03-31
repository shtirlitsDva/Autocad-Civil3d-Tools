using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

using Dreambuild.AutoCAD;

using IntersectUtilities.UtilsCommon.DataManager;
using IntersectUtilities.UtilsCommon;

using System;
using System.Linq;

using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.UtilsCommon.Utils;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>APCREATEV2</command>
        /// <summary>
        /// Creates automatic pipe profile for longitudinal sections using the pipe solver service.
        /// </summary>
        /// <category>Longitudinal Profiles</category>
        [CommandMethod("APCREATEV2")]
        public void apcreatev2()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            string devLyr = "AutoProfileTest";
            localDb.CheckOrCreateLayer(devLyr, 1, false);

            string apLayer = "AutoProfile";
            localDb.CheckOrCreateLayer(apLayer, 1, false);

            AutoProfileV2PipelineCollector.ClearDebugLayer(localDb, devLyr);

            var dcd = new PSetDefs.DriCrossingData();
            PropertySetManager.UpdatePropertySetDefinition(localDb, dcd.SetName);

            var dro = DataReferencesOptions.Create();
            if (dro == null) return;

            var dm = new DataManager(dro);
            using Database fjvDb = dm.Fremtid();
            using Transaction fjvTx = fjvDb.TransactionManager.StartTransaction();
            using Transaction tx = localDb.TransactionManager.StartTransaction();
            PropertySetManager psm = new PropertySetManager(localDb, dcd.SetName);
            PropertySetHelper pshFjv = new(fjvDb);

            try
            {
                var pipelines = AutoProfileV2PipelineCollector.Collect(
                    localDb, tx, fjvDb, fjvTx, psm, pshFjv, dcd, prdDbg);

                using var solverClient = new AutoProfileV2SolverClient(prdDbg);

                foreach (var pipeline in pipelines.OrderBy(x => x.Name))
                {
                    prdDbg($"Solving {pipeline.Name} with pipe solver service");
                    System.Windows.Forms.Application.DoEvents();

                    var profilePolyline = solverClient.SolveProfilePolyline(pipeline);
                    profilePolyline.Color = ColorByName("red");
                    profilePolyline.ConstantWidth = 0.07;
                    profilePolyline.Layer = apLayer;
                    profilePolyline.AddEntityToDbModelSpace(localDb);
                }
            }
            catch (DebugEntityException dex)
            {
                tx.Abort();
                prdDbg(dex);

                if (dex.DebugEntities != null && dex.DebugEntities.Count > 0)
                {
                    using Transaction dtx = localDb.TransactionManager.StartTransaction();
                    foreach (var ent in dex.DebugEntities)
                    {
                        ent.Layer = devLyr;
                        ent.AddEntityToDbModelSpace(localDb);
                    }
                    dtx.Commit();
                }

                return;
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                prdDbg(ex);
                return;
            }
            finally
            {
                fjvTx.Abort();
            }

            tx.Commit();
            prdDbg("Done!");
        }
    }
}
