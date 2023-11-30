using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

using psh = IntersectUtilities.PropertySetPipelineGraphHelper;

namespace IntersectUtilities.Collections
{
    #region Collections
    public class EntityCollection : List<Entity>
    {
        private static Regex conRgx = new Regex(@"(?<OwnEndType>\d):(?<ConEndType>\d):(?<Handle>\w*);");
        public EntityCollection() : base() { }
        public EntityCollection(IEnumerable<Entity> ents) : base(ents) { }
        #region IsConnectedTo implementation
        public bool IsConnectedTo(EntityCollection other)
        {
            if (this.Count == 0 || other.Count == 0) return false;
            prdDbg(psh.Pipeline.ReadPropertyString(this.First(), psh.PipelineDef.BelongsToAlignment));
            prdDbg(string.Join(">", this.Select(x => ReadConnection(x))));
            prdDbg(string.Join("<", this.Where(x => ReadConnection(x).IsNoE()).Select(x => x.Handle)));
            return this.GetAllOtherHandles().Any(x => other.Any(y => x == y.Handle.ToString()));
        }
        private string ReadConnection(Entity ent) =>
            psh.Graph.ReadPropertyString(ent, psh.GraphDef.ConnectedEntities);
        private IEnumerable<string> GetOtherHandlesFromString(string connectionString)
        {
            string[] conns = connectionString.Split(';');
            foreach (var item in conns)
                if (conRgx.IsMatch(item))
                    yield return conRgx.Match(item).Groups["Handle"].Value;
        }
        private IEnumerable<string> GetAllOtherHandles() =>
            this.Select(x => GetOtherHandlesFromString(ReadConnection(x))).SelectMany(x => x);
        #endregion
        public int GetMaxDN()
        {
            return this.Max(x =>
            {
                switch (x)
                {
                    case Polyline _:
                        return GetPipeDN(x);
                    case BlockReference br:
                        return Convert.ToInt32(
                            br.ReadDynamicCsvProperty(DynamicProperty.DN1, CsvData.Get("fjvKomponenter")));
                    default:
                        return 0;
                }
            });
        }
    }
    #endregion

    #region Extensions
    public static class EntityCollectionExtensions
    {
        public static void Partition(
            this IEnumerable<Entity> ents, Func<Entity, bool> predicate,
            out EntityCollection trueEnts, out EntityCollection falseEnts)
        {
            trueEnts = new EntityCollection();
            falseEnts = new EntityCollection();
            foreach (var ent in ents)
                if (predicate(ent)) trueEnts.Add(ent);
                else falseEnts.Add(ent);
        }
    } 
    #endregion
}
