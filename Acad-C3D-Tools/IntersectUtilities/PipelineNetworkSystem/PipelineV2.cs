using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.Collections;
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
using psh = IntersectUtilities.PropertySetPipelineGraphHelper;

namespace IntersectUtilities.PipelineNetworkSystem
{
    public interface IPipelineV2
    {
        string Name { get; }
        string Label { get; }
        EntityCollection Entities { get; }
        EntityCollection Welds { get; }
        int GetMaxDN();
        bool IsConnectedTo(IPipelineV2 other, double tol);
    }
    public abstract class PipelineV2Base : IPipelineV2
    {
        private EntityCollection ents;
        private EntityCollection welds;
        public EntityCollection Entities { get => ents; }
        public EntityCollection Welds { get => welds; }
        public abstract string Name { get; }
        public virtual string Label { get => $"\"{Name}\""; }
        public PipelineV2Base(IEnumerable<Entity> source)
        {
            source.Partition(IsNotWeld, out this.ents, out this.welds);

            bool IsNotWeld(Entity e) => 
                e is BlockReference br && 
                br.ReadDynamicCsvProperty(
                    DynamicProperty.Type, CsvData.Get("fjvKomponenter"), false) != "Svejsning";
        }
        public int GetMaxDN() => ents.GetMaxDN();
        public abstract bool IsConnectedTo(IPipelineV2 other, double tol);
    }
    public class PipelineV2Alignment : PipelineV2Base
    {
        private Alignment al;
        public PipelineV2Alignment(IEnumerable<Entity> ents, Alignment al) : base(ents)
        {
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
                    return this.Entities.IsConnectedTo(pna.Entities);
                default:
                    throw new Exception($"Unknown pipeline type {other.GetType()}!");
            }
        }
    }
    public class PipelineV2Na : PipelineV2Base
    {
        public PipelineV2Na(IEnumerable<Entity> ents) : base(ents) {}
        public override string Name =>
            psh.Pipeline.ReadPropertyString(
                this.Entities.First(), psh.PipelineDef.BelongsToAlignment);
        public override bool IsConnectedTo(IPipelineV2 other, double tol) =>
            this.Entities.IsConnectedTo(other.Entities);
    }
    public static class PipelineV2Factory
    {
        public static IPipelineV2 Create(IEnumerable<Entity> ents, Alignment al)
        {
            if (al == null) return new PipelineV2Na(ents);
            else return new PipelineV2Alignment(ents, al);
        }
    }
}
