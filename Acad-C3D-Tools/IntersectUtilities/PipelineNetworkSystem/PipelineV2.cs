using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using MoreLinq;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using psh = IntersectUtilities.PipelineNetworkSystem.PropertySetHelper;

namespace IntersectUtilities.PipelineNetworkSystem
{
    public interface IPipelineV2
    {
        string Name { get; }
        string Label { get; }
        EntityCollection Entities { get; }
        int GetMaxDN();
        bool IsConnectedTo(IPipelineV2 other, double tol);
    }
    public abstract class PipelineV2Base : IPipelineV2
    {
        protected EntityCollection ents;
        public EntityCollection Entities { get => ents; }
        public abstract string Name { get; }
        public virtual string Label { get => $"\"{Name}\""; }
        public int GetMaxDN() => ents.GetMaxDN();
        public abstract bool IsConnectedTo(IPipelineV2 other, double tol);
    }
    public class PipelineV2Alignment : PipelineV2Base
    {
        private Alignment al;
        public PipelineV2Alignment(IEnumerable<Entity> ents, Alignment al)
        {
            this.ents = new EntityCollection(ents);
            this.al = al;
        }
        public override string Name => al.Name;
        public override bool IsConnectedTo(IPipelineV2 other, double tol)
        {
            switch (other)
            {
                case PipelineV2Alignment pal:
                    return this.al.IsConnectedTo(pal.al, tol);
                case PipelineV2Na pna:
                    return this.ents.IsConnectedTo(pna.Entities);
                default:
                    throw new Exception($"Unknown pipeline type {other.GetType()}!");
            }
        }
    }
    public class PipelineV2Na : PipelineV2Base
    {
        public PipelineV2Na(IEnumerable<Entity> ents)
        {
            this.ents = new EntityCollection(ents);
        }
        public override string Name =>
            psh.Pipeline.ReadPropertyString(
                ents.First(), psh.PipelineDef.BelongsToAlignment);
        public override bool IsConnectedTo(IPipelineV2 other, double tol) =>
            this.ents.IsConnectedTo(other.Entities);
    }
    public static class PipelineV2Factory
    {
        public static IPipelineV2 Create(IEnumerable<Entity> ents, Alignment al)
        {
            if (al == null)
            {
                return new PipelineV2Na(ents);
            }
            else
            {
                return new PipelineV2Alignment(ents, al);
            }
        }
    }
    public class EntityCollection : List<Entity>
    {
        private static Regex conRgx = new Regex(@"(?<OwnEndType>\d):(?<ConEndType>\d):(?<Handle>\w*);");
        public EntityCollection(IEnumerable<Entity> ents) : base(ents) { }
        #region IsConnectedTo implementation
        public bool IsConnectedTo(EntityCollection other)
        {
            if (this.Count == 0 || other.Count == 0) return false;
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
}
