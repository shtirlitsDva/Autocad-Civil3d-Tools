using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

using NTRExport.TopologyModel;

using static IntersectUtilities.UtilsCommon.Utils;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(NTRExport.Spines.SpineCommands))]

namespace NTRExport.Spines
{
    public sealed class SpineCommands : IExtensionApplication
    {
        public void Initialize()
        {
            // no-op
        }

        public void Terminate()
        {
            // no-op
        }

        [CommandMethod("NTRSPINE")]
        public void BuildSpines()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using var tx = localDb.TransactionManager.StartTransaction();
            try
            {
                var ents = localDb.GetFjvEntities(tx);
                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();

                var topo = TopologyBuilder.Build(polylines, fittings);

                var builder = new SpineBuilder();
                var spines = builder.Build(topo);

                prdDbg($"Spines built: {spines.Count}");

                double totalLen = 0.0;
                foreach (var s in spines) totalLen += s.TotalLength;
                prdDbg($"Total spine length (plan): {totalLen:0.###} m");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }
    }
}


