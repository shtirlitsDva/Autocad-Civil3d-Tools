using Autodesk.AutoCAD.DatabaseServices;

using GroupByCluster;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Graphs;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;

using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.GraphWrite
{
    /// <summary>
    /// Writes and clears the DriGraph connections of a drawing's FJV entities.
    /// Holds no command, so a tool in another assembly can compile it in as
    /// source (NorsynDrawingTools' legacy import links this file).
    /// </summary>
    public static class GraphPopulation
    {
        /// <summary>
        /// Writes every FJV entity's DriGraph ConnectedEntities from the points
        /// where it meets the others, in its own committed transaction. Throws
        /// when it fails, and then writes nothing.
        /// </summary>
        public static void Populate(Database localDb)
        {
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var komponenter = Csv.FjvDynamicComponents;
                    HashSet<Entity> allEnts = localDb.GetFjvEntities(tx, true, false);
                    PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriGraph);
                    var graph = new Graph(localDb, psm, komponenter);
                    foreach (Entity entity in allEnts) graph.AddEntityToPOIs(entity);
                    //Create clusters of POIs based on a maximum distance
                    //Distance is reduced, because was having a bad day
                    IEnumerable<IGrouping<POI, POI>> clusters
                        = graph.POIs.GroupByCluster((x, y) => x.Point.GetDistanceTo(y.Point), 0.003);
                    //Iterate over clusters
                    foreach (IGrouping<POI, POI> cluster in clusters)
                    {
                        //Create unique pairs
                        var pairs = cluster.SelectMany((value, index) => cluster.Skip(index + 1),
                                                       (first, second) => new { first, second });
                        //Create reference to each other for each pair
                        foreach (var pair in pairs)
                        {
                            if (pair.first.Owner.Handle == pair.second.Owner.Handle) continue;
                            pair.first.AddReference(pair.second);
                            pair.second.AddReference(pair.first);
                        }
                    }
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
        /// Empties every FJV entity's DriGraph ConnectedEntities, in its own
        /// committed transaction. Throws when it fails, and then writes nothing.
        /// </summary>
        public static void Clear(Database localDb)
        {
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Entity> allEnts = localDb.GetFjvEntities(tx, true, false);
                    PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriGraph);
                    PSetDefs.DriGraph driGraph = new PSetDefs.DriGraph();
                    foreach (var item in allEnts)
                        psm.WritePropertyString(item, driGraph.ConnectedEntities, "");
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
