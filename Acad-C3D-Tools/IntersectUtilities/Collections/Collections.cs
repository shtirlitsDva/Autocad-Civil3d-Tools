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
    public class EntityCollection : ICollection<Entity>
    {
        public Entity this[int index] { get => _L[index]; set => _L[index] = value; }
        private static Regex conRgx = new Regex(@"(?<OwnEndType>\d):(?<ConEndType>\d):(?<Handle>\w*)");
        private List<Entity> _L = new List<Entity>();
        //private HashSet<Handle> otherHandles = new HashSet<Handle>();
        private List<Handle> otherHandles = new List<Handle>();
        public EntityCollection() { }
        public EntityCollection(IEnumerable<Entity> ents)
        {
            foreach (var ent in ents)
            {
                foreach (var handle in GetOtherHandlesFromString(ReadConnection(ent)))
                {
                    if (handle.IsNoE()) continue;
                    Handle h = new Handle(Convert.ToInt64(handle, 16));
                    otherHandles.Add(h);
                }
            }
        }
        public void Add(Entity item)
        {
            _L.Add(item);
            foreach (var handle in GetOtherHandlesFromString(ReadConnection(item)))
            {
                if (handle.IsNoE()) continue;
                Handle h = new Handle(Convert.ToInt64(handle, 16));
                if (item.Handle.ToString() == "19556B") prdDbg(h);
                otherHandles.Add(h);
            }
        }
        #region Custom logic
        public bool IsConnectedTo(EntityCollection other)
        {
            if (this.Count == 0 || other.Count == 0) return false;
            if ((psh.Pipeline.ReadPropertyString(this[0], psh.PipelineDef.BelongsToAlignment) == "NA 01" ||
                psh.Pipeline.ReadPropertyString(other[0], psh.PipelineDef.BelongsToAlignment) == "NA 01") &&
                (psh.Pipeline.ReadPropertyString(this[0], psh.PipelineDef.BelongsToAlignment) == "05" ||
                psh.Pipeline.ReadPropertyString(other[0], psh.PipelineDef.BelongsToAlignment) == "05"))
            {
                prdDbg("this");
                prdDbg(this.Count + " " + otherHandles.Count);
                prdDbg(string.Join("\n", otherHandles.Select(x => x.ToString())));
                prdDbg("other");
                prdDbg(string.Join("\n", other.Select(x => x.Handle.ToString())));

                prdDbg(this.otherHandles.Any(x => other.Any(y => x == y.Handle)));
            }
            return this.otherHandles.Any(x => other.Any(y => x == y.Handle));
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
        public int GetMaxDN()
        {
            if (this.Count == 0) return 0;
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
        #endregion
        // Implement other members of ICollection<T>
        public void Clear() => _L.Clear();
        public bool Contains(Entity item) => _L.Contains(item);
        public void CopyTo(Entity[] array, int arrayIndex) => _L.CopyTo(array, arrayIndex);
        public bool Remove(Entity item) => _L.Remove(item);
        public int Count => _L.Count;
        public bool IsReadOnly => false;
        public IEnumerator<Entity> GetEnumerator() => _L.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
    #endregion

    #region Extensions
    public static class EntityCollectionExtensions
    {
        public static void Partition(
            this IEnumerable<Entity> source, Func<Entity, bool> predicate,
            out EntityCollection trueEnts, out EntityCollection falseEnts)
        {
            trueEnts = new EntityCollection();
            falseEnts = new EntityCollection();
            foreach (var ent in source)
                if (predicate(ent)) trueEnts.Add(ent);
                else falseEnts.Add(ent);
        }
    }
    #endregion
}
