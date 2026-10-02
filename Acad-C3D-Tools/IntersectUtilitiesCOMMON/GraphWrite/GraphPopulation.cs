#if BRICSCAD
using Teigha.DatabaseServices;
#else
using Autodesk.AutoCAD.DatabaseServices;
#endif

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
    /// Writes and clears the DriGraph connections of a drawing's FJV entities,
    /// through whatever property-set access the caller brings: AEC's in
    /// IntersectUtilities, NorsynDrawingTools' in-memory one for its legacy
    /// import. Holds no command.
    /// </summary>
    public static partial class GraphPopulation
    {
        /// <summary>
        /// Writes every FJV entity's DriGraph ConnectedEntities from the points
        /// where it meets the others.
        /// </summary>
        public static void Populate(Database localDb, Transaction tx, PropertySetHelper psh)
        {
            var komponenter = Csv.FjvDynamicComponents;
            HashSet<Entity> allEnts = localDb.GetFjvEntities(tx, true, false);
            var graph = new Graph(localDb, psh, komponenter);
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

        /// <summary>
        /// Empties every FJV entity's DriGraph ConnectedEntities.
        /// </summary>
        public static void Clear(Database localDb, Transaction tx, PropertySetHelper psh)
        {
            HashSet<Entity> allEnts = localDb.GetFjvEntities(tx, true, false);
            foreach (var item in allEnts)
                psh.Graph.WritePropertyString(item, psh.GraphDef.ConnectedEntities, "");
        }
    }
}
