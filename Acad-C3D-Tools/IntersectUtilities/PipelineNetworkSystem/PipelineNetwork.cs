using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static IntersectUtilities.UtilsCommon.Utils;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.PipelineNetworkSystem
{
    public class PipelineNetwork
    {
        private HashSet<IPipelineV2> pipelines;

        public void CreatePipeNetwork(IEnumerable<Entity> ents, IEnumerable<Alignment> als)
        {
            pipelines = new HashSet<IPipelineV2>();

            Database db = ents?.FirstOrDefault()?.Database;
            if (db == null) throw new Exception(
                "Either ents collection, first element or its' database is null!");

            PropertySetManager psmGraph = new PropertySetManager(db, PSetDefs.DefinedSets.DriGraph);
            PSetDefs.DriGraph graphDef = new PSetDefs.DriGraph();
            PropertySetManager psmPpld = new PropertySetManager(db, PSetDefs.DefinedSets.DriPipelineData);
            PSetDefs.DriPipelineData ppldDef = new PSetDefs.DriPipelineData();

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // Get all the names of the pipelines
                    // because we need to create a network for each of them
                    // we also need to get the names of parts that do not belong to any pipeline
                    // ie. NA XX parts
                    var pplNames = ents.Select(
                        e => psmPpld.ReadPropertyString(
                            e, ppldDef.BelongsToAlignment)).Distinct();
                    
                    // Create a network to be able to analyze our piping system

                }
                catch (Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    throw;
                }
                tx.Commit();
            }
        }
    }
}
