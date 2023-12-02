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
        public IEnumerable<Handle> ExternalHandles 
        { 
            get => _L.SelectMany(x => GetOtherHandles(ReadConnection(x))).Where(x => _L.All(y => y.Handle != x)).Distinct(); 
        }
        public EntityCollection() { }
        public EntityCollection(IEnumerable<Entity> ents)
        {
            _L.AddRange(ents);
        }
        public void Add(Entity item)
        {
            _L.Add(item);
        }
        #region Custom logic
        public IEnumerable<Entity> GetPolylines() => _L.Where(x => x is Polyline);
        public IEnumerable<Entity> GetBlockReferences() => _L.Where(x => x is BlockReference);
        public bool IsConnectedTo(EntityCollection other)
        {
            if (this.Count == 0 || other.Count == 0) return false;
            return this.ExternalHandles.Any(x => other.Any(y => x == y.Handle));
        }
        private string ReadConnection(Entity ent) =>
            psh.Graph.ReadPropertyString(ent, psh.GraphDef.ConnectedEntities);
        private IEnumerable<Handle> GetOtherHandles(string connectionString)
        {
            string[] conns = connectionString.Split(';');
            foreach (var item in conns)
                if (conRgx.IsMatch(item))
                    yield return new Handle(
                        Convert.ToInt64(conRgx.Match(item).Groups["Handle"].Value, 16));
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
