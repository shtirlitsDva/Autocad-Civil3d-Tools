using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;
using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;

namespace IntersectUtilities.Collections
{
    #region EntityCollection
    public class EntityCollection : ICollection<Entity>
    {
        public Entity this[int index] { get => _L[index]; set => _L[index] = value; }
        private static Regex conRgx = new Regex(@"(?<OwnEndType>\d):(?<ConEndType>\d):(?<Handle>\w*)");
        private List<Entity> _L = new List<Entity>();
        private Dictionary<Handle, HashSet<Handle>> _C = new Dictionary<Handle, HashSet<Handle>>();
        private PropertySetHelper psh;
        public IEnumerable<Handle> ExternalHandles
        {
            get => _L.SelectMany(x => GetOtherHandles(ReadConnection(x))).Where(x => _L.All(y => y.Handle != x)).Distinct();
        }
        public EntityCollection() { }
        public EntityCollection(IEnumerable<Entity> ents)
        {
            if (psh == null) psh = new PropertySetHelper(
                ents.First().Database);

            _L.AddRange(ents);
            foreach (var ent in ents)
                _C.Add(ent.Handle, new HashSet<Handle>(GetOtherHandles(ReadConnection(ent))));
        }
        public void Add(Entity item)
        {
            if (psh == null) psh =
                    new PropertySetHelper(item.Database);

            _L.Add(item);
            _C.Add(item.Handle, new HashSet<Handle>(GetOtherHandles(ReadConnection(item))));
        }
        #region Custom logic
        public IEnumerable<Polyline> GetPolylines() => _L.Where(x => x is Polyline).Cast<Polyline>();
        public IEnumerable<BlockReference> GetBlockReferences() => _L.Where(x => x is BlockReference).Cast<BlockReference>();
        public bool IsConnectedTo(EntityCollection other)
        {
            if (this.Count == 0 || other.Count == 0) return false;
            return this._C.SelectMany(x => x.Value).Any(x => other.Any(y => x == y.Handle));
        }
        public Point3d GetConnectionPoint(Entity ent)
        {
            var otherHandles = GetOtherHandles(ReadConnection(ent));
            var entFromThisCollection =
                this.FirstOrDefault(x => otherHandles.Contains(x.Handle));

            if (entFromThisCollection == null) return Point3d.Origin;

            var psHere = entFromThisCollection.GetAllEndPoints();
            var psThere = ent.GetAllEndPoints();
            if (psHere.Any(x => psThere.Any(y => x.HorizontalEqualz(y))))
                return psHere.First(x => psThere.Any(y => x.HorizontalEqualz(y)));
            else return Point3d.Origin;
        }
        public Entity GetEntityByPoint(Point3d pt)
        {
            Dictionary<Point3d, Entity> dict = new Dictionary<Point3d, Entity>(new Point3dHorizontalComparer());
            //It should be safe enought to use TryAdd here, as there can't be two elements with the same horizontal coordinates.
            //at the same point connecting to the same entity.
            foreach (var item in this)
                foreach (var p in item.GetAllEndPoints()) dict.TryAdd(p, item);

            if (dict.TryGetValue(pt, out Entity value))
            {
                return value;
            }

            return null;
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
                        string type = br.ReadDynamicCsvProperty(DynamicProperty.Type, false);
                        if (type == "Afgreningsstuds" || type == "Svanehals")
                            return Convert.ToInt32(br.ReadDynamicCsvProperty(DynamicProperty.DN2));
                        else return Convert.ToInt32(br.ReadDynamicCsvProperty(DynamicProperty.DN1));
                    default:
                        return 0;
                }
            });
        }
        public bool HasSizeArrayBrs()
        {
            if (this.Count == 0) return false;
            return this.Any(x =>
            {
                switch (x)
                {
                    case Polyline _:
                        return false;
                    case BlockReference br:
                        return br.ReadDynamicCsvProperty(
                            DynamicProperty.Function) == "SizeArray";
                    default:
                        return false;
                }
            });
        }
        /// <summary>
        /// Assuming ent is a SizeArray block.
        /// That is reducer, X-model or materialeskift.
        /// </summary>
        public List<HashSet<Entity>> GetConnectedEntitiesDelimited(Entity ent)
        {
            List<HashSet<Entity>> res = new List<HashSet<Entity>>();

            if (this.Count == 0)
            {
                prdDbg($"EntityCollection for Entity {ent.Handle} is empty!");
                return res;
            }

            Database db = ent.Database;

            var otherHandles = GetOtherHandles(ReadConnection(ent)).ToArray();

            #region Validity check
            if (otherHandles.Length == 0)
            {
                prdDbg($"Entity {ent.Handle} does not have any entities connected!");
                return res;
            }
            if (otherHandles.Length > 3)
            {
                prdDbg($"Entity {ent.Handle} has more than 3 entities connected!");
                return res;
            }
            #endregion

            for (int i = 0; i < otherHandles.Length; i++)
            {
                HashSet<Entity> col = new HashSet<Entity>();
                Stack<Entity> stack = new Stack<Entity>();
                stack.Push(otherHandles[i].Go<Entity>(db));

                while (stack.Count > 0)
                {
                    var item = stack.Pop();

                    if (item is BlockReference br)
                        if (br.ReadDynamicCsvProperty(
                            DynamicProperty.Function) == "SizeArray")
                            continue;

                    col.Add(item);

                    var otherHandlesCur = GetOtherHandles(ReadConnection(item));
                    foreach (var handle in otherHandlesCur)
                    {
                        if (col.Any(x => x.Handle == handle)) continue;
                        var curEnt = handle.Go<Entity>(db);
                        stack.Push(curEnt);
                    }
                }

                res.Add(col);
            }

            return res;
        }
        #endregion
        // Implement other members of ICollection<T>
        public void Clear() { _L.Clear(); _C.Clear(); }
        public bool Contains(Entity item) => _L.Contains(item);
        public void CopyTo(Entity[] array, int arrayIndex) => throw new NotImplementedException();
        public bool Remove(Entity item) => _L.Remove(item) && _C.Remove(item.Handle);
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
        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= _L.Count) return false;
            _L.RemoveAt(index);
            return true;
        }
        public IEnumerator<SizeEntryV2> GetEnumerator() => _L.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
    #endregion
}
