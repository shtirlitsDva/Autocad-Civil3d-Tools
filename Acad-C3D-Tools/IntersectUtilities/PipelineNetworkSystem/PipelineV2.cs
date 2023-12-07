using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
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
        void AutoReversePolylines(IPipelineV2 parent, IEnumerable<IPipelineV2> children);
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
                (e is BlockReference br &&
                br.ReadDynamicCsvProperty(
                    DynamicProperty.Type, CsvData.Get("fjvKomponenter"), false) != "Svejsning") ||
                    e is Polyline; //<-- this is the culprit!!!!!!!!!!!!!!!!!!!!
        }
        public int GetMaxDN() => ents.GetMaxDN();
        public abstract bool IsConnectedTo(IPipelineV2 other, double tol);
        public abstract void AutoReversePolylines(IPipelineV2 parent, IEnumerable<IPipelineV2> children);
    }
    public class PipelineV2Alignment : PipelineV2Base
    {
        private Alignment al;
        public PipelineV2Alignment(IEnumerable<Entity> ents, Alignment al) : base(ents)
        {
            this.al = al;
        }
        public override string Name => al.Name;

        public override void AutoReversePolylines(IPipelineV2 parent, IEnumerable<IPipelineV2> children)
        {
            var refPline = al.GetPolyline().Go<Polyline>(
                al.Database.TransactionManager.TopTransaction);
            var plines = Entities.GetPolylines().Cast<Polyline>();

            // Determine the direction of the alignment
            PipelineSizeArray sizes = new PipelineSizeArray(al,
                Entities.GetPolylines().Cast<Curve>().ToHashSet(),
                Entities.GetBlockReferences().Cast<BlockReference>().ToHashSet());

            if (parent == null)
            {
                // if parent is null, which means this is an entry pipeline
                if (sizes.Arrangement == PipelineSizeArray.PipelineSizesArrangement.OneSize)
                {
                    //This is a hard one, because we cannot determine the direction of the pipeline
                    //Right now we will just assume direction following the alignment stations

                    foreach (Polyline pline in plines)
                    {
                        Point3d pS = refPline.GetClosestPointTo(pline.StartPoint, false);
                        Point3d pE = refPline.GetClosestPointTo(pline.EndPoint, false);

                        double sS = al.StationAtPoint(pS);
                        double sE = al.StationAtPoint(pE);

                        if (sS > sE)
                        {
                            pline.UpgradeOpen();
                            pline.ReverseCurve();
                        }
                    }
                }
                else if (sizes.Arrangement == PipelineSizeArray.PipelineSizesArrangement.MiddleDescendingToEnds)
                    throw new Exception("MiddleDescendingToEnds for a root pipeline is not supported!");
                else
                {
                    switch (sizes.Arrangement)
                    {
                        case PipelineSizeArray.PipelineSizesArrangement.SmallToLargeAscending:

                            break;
                        case PipelineSizeArray.PipelineSizesArrangement.LargeToSmallDescending:
                            break;
                        default:
                            throw new Exception($"{sizes.Arrangement} should be handled elsewhere!");    
                    }
                }
            }
            else
            {
                if (!(parent is PipelineV2Alignment))
                    throw new Exception(
                        $"Parent {parent.Name} is not an alignment pipeline, but is {parent.GetType().Name}!\n" +
                        $"This is not supported.");

                if (sizes.Arrangement == PipelineSizeArray.PipelineSizesArrangement.MiddleDescendingToEnds)
                {

                }
                else
                {
                    var pal = parent as PipelineV2Alignment;

                }
            }

            refPline.UpgradeOpen();
            refPline.Erase(true);
        }

        private bool PointIsOn(Alignment al, Point3d point, double tol)
        {
            Polyline pline = al.GetPolyline().Go<Polyline>(
                al.Database.TransactionManager.TopTransaction);

            Point3d p = pline.GetClosestPointTo(point, false);
            pline.UpgradeOpen();
            pline.Erase(true);
            //prdDbg($"{offset}, {Math.Abs(offset)} < {tolerance}, {Math.Abs(offset) <= tolerance}, {station}");

            // If the offset is within the tolerance, the point is on the alignment
            if (Math.Abs(p.DistanceTo(point)) <= tol) return true;

            // Otherwise, the point is not on the alignment
            return false;
        }

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
        public PipelineV2Na(IEnumerable<Entity> ents) : base(ents) { }
        public override string Name =>
            psh.Pipeline.ReadPropertyString(
                this.Entities.First(), psh.PipelineDef.BelongsToAlignment);

        public override void AutoReversePolylines(IPipelineV2 parent, IEnumerable<IPipelineV2> children)
        {
            throw new NotImplementedException();
        }

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
