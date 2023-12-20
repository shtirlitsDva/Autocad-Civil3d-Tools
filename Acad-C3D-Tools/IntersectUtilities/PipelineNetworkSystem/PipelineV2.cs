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

namespace IntersectUtilities.PipelineNetworkSystem
{
    public interface IPipelineV2
    {
        string Name { get; }
        string Label { get; }
        double EndStation { get; }
        EntityCollection Entities { get; }
        EntityCollection Welds { get; }
        IPipelineSizeArrayV2 Sizes { get; }
        void CreateSizeArray();
        int GetMaxDN();
        double GetPolylineStartStation(Polyline pl);
        double GetPolylineEndStation(Polyline pl);
        double GetBlockStation(BlockReference br);
        /// <summary>
        /// The collection of entities is limited by SizeArray Function blocks.
        /// That is collection stops at the first SizeArray Function block ecountered.
        /// </summary>
        List<HashSet<Entity>> GetEntsToEachSideOf(Entity ent);
        bool IsConnectedTo(IPipelineV2 other, double tol);
        void AutoReversePolylines(IPipelineV2 parent, IEnumerable<IPipelineV2> children);
        IEnumerable<Entity> GetEntitiesWithinStations(double start, double end);
        void CorrectPipesToCutLengths(Point3d connectionLocation);
    }
    public abstract class PipelineV2Base : IPipelineV2
    {
        protected EntityCollection ents;
        protected EntityCollection welds;
        protected IPipelineSizeArrayV2 sizes;
        protected PropertySetHelper psh;
        public EntityCollection Entities { get => ents; }
        public EntityCollection Welds { get => welds; }
        public abstract string Name { get; }
        public virtual string Label { get => $"\"{Name}\""; }
        public abstract double EndStation { get; }
        public IPipelineSizeArrayV2 Sizes => sizes;
        public PipelineV2Base(IEnumerable<Entity> source)
        {
            source.Partition(IsNotWeld, out this.ents, out this.welds);

            if (psh == null)
                psh = new PropertySetHelper(ents?.FirstOrDefault()?.Database);

            bool IsNotWeld(Entity e) =>
                (e is BlockReference br &&
                br.ReadDynamicCsvProperty(
                    DynamicProperty.Type, false) != "Svejsning") ||
                    e is Polyline; //<-- this is the culprit!!!!!!!!!!!!!!!!!!!!
        }
        public int GetMaxDN() => ents.GetMaxDN();
        public abstract bool IsConnectedTo(IPipelineV2 other, double tol);
        public abstract void AutoReversePolylines(IPipelineV2 parent, IEnumerable<IPipelineV2> children);
        public abstract double GetPolylineStartStation(Polyline pl);
        public abstract double GetPolylineEndStation(Polyline pl);
        public abstract double GetBlockStation(BlockReference br);
        public List<HashSet<Entity>> GetEntsToEachSideOf(Entity ent)
        {
            return Entities.GetConnectedEntitiesDelimited(ent);
        }
        public abstract IEnumerable<Entity> GetEntitiesWithinStations(double start, double end);
        public void CreateSizeArray()
        {
            sizes = PipelineSizeArrayFactory.CreateSizeArray(this);
        }
        public void CorrectPipesToCutLengths(Point3d connectionLocation)
        {
            if (psh == null) psh = new PropertySetHelper(ents?.FirstOrDefault()?.Database);

            Database localDb = ents.FirstOrDefault()?.Database;
            Transaction tx = localDb.TransactionManager.TopTransaction;

            Queue<Polyline> orderedPlines;

            // First from right to left
            double curStart = 0;
            double curEnd = 0;

            var query = this.GetEntitiesWithinStations(curStart, curEnd);
        }
    }
    public class PipelineV2Alignment : PipelineV2Base
    {
        private Alignment al;
        public PipelineV2Alignment(IEnumerable<Entity> ents, Alignment al) : base(ents)
        {
            this.al = al;
        }
        public override string Name => al.Name;
        public override double EndStation => al.EndingStation;
        public override void AutoReversePolylines(IPipelineV2 parent, IEnumerable<IPipelineV2> children)
        {
            var refPline = al.GetPolyline().Go<Polyline>(
                al.Database.TransactionManager.TopTransaction);
            var plines = Entities.GetPolylines();

            IPipelineSizeArrayV2 sizes = PipelineSizeArrayFactory.CreateSizeArray(this);

            if (parent == null)
            {
                // if parent is null, which means this is an entry pipeline
                if (sizes.Arrangement == PipelineSizesArrangement.OneSize)
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
                else if (sizes.Arrangement == PipelineSizesArrangement.MiddleDescendingToEnds)
                    throw new Exception("MiddleDescendingToEnds for a root pipeline is not supported!");
                else
                {
                    switch (sizes.Arrangement)
                    {
                        case PipelineSizesArrangement.SmallToLargeAscending:

                            break;
                        case PipelineSizesArrangement.LargeToSmallDescending:
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

                if (sizes.Arrangement == PipelineSizesArrangement.MiddleDescendingToEnds)
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
        public override double GetPolylineStartStation(Polyline pl) => al.StationAtPoint(pl.StartPoint);
        public override double GetPolylineEndStation(Polyline pl) => al.StationAtPoint(pl.EndPoint);
        public override double GetBlockStation(BlockReference br) => al.StationAtPoint(br.Position);
        public override IEnumerable<Entity> GetEntitiesWithinStations(double start, double end)
        {
            foreach (var ent in this.ents)
            {
                switch (ent)
                {
                    case Polyline pl:
                        {
                            double st = al.StationAtPoint(pl.GetPointAtDist(pl.Length / 2));
                            if (st >= start && st <= end) yield return ent;
                        }
                        break;
                    case BlockReference br:
                        {
                            double st = al.StationAtPoint(br.Position);
                            if (st >= start && st <= end) yield return ent; 
                        }
                        break;
                }
            }
        }
    }
    public class PipelineV2Na : PipelineV2Base
    {
        // This is a pipeline that does not belong to any alignment
        // All connected elements are grouped into this pipeline
        // !!!! it is assumed that this pipeline only connects to other pipelines at the start
        // !!!! that is it does not connect to other pipelines (which then must be an alignment pipeline) at the end
        // !!!! because if it wasn't an alignment pipeline, it would be grouped into this pipeline
        private Polyline topology;
        public PipelineV2Na(IEnumerable<Entity> source) : base(source)
        {
            #region Preparation to have stations for NA pipelines
            ////Access ALL objects in database because we don't know our parent
            ////Filter out the objects of the pipeline in question
            //Database db = ents.FirstOrDefault()?.Database;
            //Transaction tx = db.TransactionManager.TopTransaction;
            //var query =
            //    db.GetFjvEntities(tx, true, false)
            //    .Where(x => ents.All(y => x.Handle != y.Handle));
            //EntityCollection allEnts = new EntityCollection(query);

            //// Now find the other element that is connected to this pipeline
            //var findConnection = allEnts.ExternalHandles.Where(x => ents.Any(y => y.Handle == x));

            //// Handle different cases, most optimal case is that there is only one connection
            //int conCount = findConnection.Count();
            //switch (conCount)
            //{
            //    case int n when n < 1:
            //        throw new Exception($"Pipeline {Name} is not connected to all other pipelines!");
            //    case 1:
            //        // Get the connected element
            //        var con = findConnection.First().Go<Entity>(db);

            //        break;
            //    case int n when n > 1:
            //        throw new Exception($"Pipeline {Name} is connected to more than one other pipeline!");
            //} 
            #endregion
        }
        public override string Name =>
            psh.Pipeline.ReadPropertyString(
                this.Entities.First(), psh.PipelineDef.BelongsToAlignment);
        public override double EndStation => topology.Length;
        public override void AutoReversePolylines(IPipelineV2 parent, IEnumerable<IPipelineV2> children)
        {
            throw new NotImplementedException();
        }
        public override double GetBlockStation(BlockReference br)
        {
            return 0;
        }
        public override double GetPolylineStartStation(Polyline pl)
        {
            return 0;
        }
        public override double GetPolylineEndStation(Polyline pl)
        {
            return 0;
        }
        public override bool IsConnectedTo(IPipelineV2 other, double tol) =>
            this.Entities.IsConnectedTo(other.Entities);
        public override IEnumerable<Entity> GetEntitiesWithinStations(double start, double end)
        {
            throw new NotImplementedException();
        }
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
