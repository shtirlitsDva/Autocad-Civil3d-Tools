using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.PipelineNetworkSystem;
using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

using psh = IntersectUtilities.PropertySetPipelineGraphHelper;

namespace IntersectUtilities.Collections
{
    #region EntityCollection
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
        public IEnumerable<Polyline> GetPolylines() => _L.Where(x => x is Polyline).Cast<Polyline>();
        public IEnumerable<BlockReference> GetBlockReferences() => _L.Where(x => x is BlockReference).Cast<BlockReference>();
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

    #endregion

    #region SizeEntryCollection
    // implement SizeEntryCollection that implements ICollection<SizeEntryV2>
    public class SizeEntryCollection : ICollection<SizeEntryV2>
    {
        private List<SizeEntryV2> _L = new List<SizeEntryV2>();
        public SizeEntryV2 this[int index] { get => _L[index]; set => _L[index] = value; }
        public SizeEntryCollection() { }
        public SizeEntryCollection(IEnumerable<SizeEntryV2> sizes)
        {
            _L.AddRange(sizes);
        }
        public void Add(SizeEntryV2 item)
        {
            _L.Add(item);
        }
        public int Count => _L.Count;
        public bool IsReadOnly => false;
        public void Clear() => _L.Clear();
        public bool Contains(SizeEntryV2 item) => _L.Contains(item);
        public void CopyTo(SizeEntryV2[] array, int arrayIndex) => _L.CopyTo(array, arrayIndex);
        public bool Remove(SizeEntryV2 item) => _L.Remove(item);
        public IEnumerator<SizeEntryV2> GetEnumerator() => _L.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
    #endregion
}
