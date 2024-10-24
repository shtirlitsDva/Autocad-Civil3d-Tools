using Autodesk.AutoCAD.ApplicationServices;
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
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using static IntersectUtilities.Graph;

namespace IntersectUtilities.PipelineNetworkSystem
{
    public interface IPipelineV2
    {
        string Name { get; }
        string Label { get; }
        double EndStation { get; }
        Point3d StartPoint { get; }
        Point3d EndPoint { get; }
        EntityCollection Entities { get; }
        EntityCollection Welds { get; }
        IPipelineSizeArrayV2 Sizes { get; }
        void CreateSizeArray();
        int GetMaxDN();
        double GetPolylineStartStation(Polyline pl);
        double GetPolylineMiddleStation(Polyline pl);
        double GetPolylineEndStation(Polyline pl);
        double GetBlockStation(BlockReference br);
        double GetStationAtPoint(Point3d pt);
        Point3d GetClosestPointTo(Point3d pt, bool extend = false);
        double GetDistanceToPoint(Point3d pt, bool extend = false);
        Vector3d GetFirstDerivative(Point3d pt);
        bool IsConnectedTo(IPipelineV2 other, double tol);
        Point3d GetConnectionLocationToParent(IPipelineV2 other, double tol);
        bool DetermineUnconnectedEndPoint(IPipelineV2 other, double tol, out Point3d freeEnd);
        void AutoReversePolylines(Point3d connectionLocation);
        IEnumerable<Entity> GetEntitiesWithinStations(double start, double end);
        IEnumerable<Polyline> GetPolylines();
        Point3d GetLocationForMaxDN();
        Result CorrectPipesToCutLengths(Point3d connectionLocation);
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
        public abstract Point3d StartPoint { get; }
        public abstract Point3d EndPoint { get; }
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
        public abstract double GetPolylineStartStation(Polyline pl);
        public abstract double GetPolylineMiddleStation(Polyline pl);
        public abstract double GetPolylineEndStation(Polyline pl);
        public abstract double GetBlockStation(BlockReference br);
        public abstract double GetStationAtPoint(Point3d pt);
        public abstract Point3d GetClosestPointTo(Point3d pt, bool extend = false);
        public abstract IEnumerable<Entity> GetEntitiesWithinStations(double start, double end);
        public void CreateSizeArray()
        {
            sizes = PipelineSizeArrayFactory.CreateSizeArray(this);
        }
        public void AutoReversePolylines(Point3d connectionLocation)
        {
            double st = GetStationAtPoint(connectionLocation);
            var againstFlow = GetEntitiesWithinStations(0, st);
            var withFlow = GetEntitiesWithinStations(st, EndStation);

            //UtilsCommon.Utils.Debug.CreateDebugLine(
            //    connectionLocation, AutocadStdColors["red"]);

            foreach (var item in againstFlow)
            {
                switch (item)
                {
                    case Polyline pl:
                        if (!isPolylineOrientedCorrectly(pl, false))
                        {
                            pl.CheckOrOpenForWrite();
                            pl.ReverseCurve();
#if DEBUG
                            //Line l = new Line(Point3d.Origin, pl.GetPointAtDist(pl.Length / 2));
                            //l.AddEntityToDbModelSpace(Application.DocumentManager.MdiActiveDocument.Database);
#endif
                        }
                        break;
                }
            }
            foreach (var item in withFlow)
            {
                switch (item)
                {
                    case Polyline pl:
                        if (!isPolylineOrientedCorrectly(pl, true))
                        {
                            pl.CheckOrOpenForWrite();
                            pl.ReverseCurve();
#if DEBUG
                            //Line l = new Line(Point3d.Origin, pl.GetPointAtDist(pl.Length / 2));
                            //l.AddEntityToDbModelSpace(Application.DocumentManager.MdiActiveDocument.Database);
#endif
                        }
                        break;
                }
            }
        }
        protected bool isPolylineOrientedCorrectly(Polyline pl, bool withFlow)
        {
            double ss = GetPolylineStartStation(pl);
            double es = GetPolylineEndStation(pl);

            if (withFlow) return ss < es;
            else return ss > es;
        }
        public abstract Point3d GetConnectionLocationToParent(IPipelineV2 parent, double tol);
        /// <summary>
        /// Determines start or end point for max DN.
        /// Cannot be used for pipelines supplied from the middle.
        /// </summary>
        public Point3d GetLocationForMaxDN()
        {
            if (sizes == null) CreateSizeArray();

            //Case one size -> return start point
            if (sizes.Length == 1) return this.StartPoint;

            if (sizes.Sizes.First().System == PipeSystemEnum.Stål &&
                sizes.Sizes.Last().System == PipeSystemEnum.Stål)
            {
                if (sizes.Sizes.First().DN > sizes.Sizes.Last().DN) return this.StartPoint;
                else return this.EndPoint;
            }
            else if (sizes.Sizes.First().System == PipeSystemEnum.Stål) return this.StartPoint;
            else if (sizes.Sizes.Last().System == PipeSystemEnum.Stål) return this.EndPoint;
            else
            {
                if (sizes.Sizes.First().DN > sizes.Sizes.Last().DN) return this.StartPoint;
                else return this.EndPoint;
            }

            throw new Exception($"Could not determine location for max DN for pipeline {this.Name}!");
        }
        public bool DetermineUnconnectedEndPoint(IPipelineV2 other, double tol, out Point3d freeEnd)
        {
            freeEnd = Point3d.Origin;

            Point3d thisStart = this.StartPoint;
            Point3d testPs = other.GetClosestPointTo(thisStart, false);

            if (thisStart.DistanceHorizontalTo(testPs) < tol)
            {
                freeEnd = this.EndPoint;
                return true;
            }

            Point3d thisEnd = this.EndPoint;
            Point3d testPe = other.GetClosestPointTo(thisEnd, false);

            if (thisEnd.DistanceHorizontalTo(testPe) < tol)
            {
                freeEnd = this.StartPoint;
                return true;
            }
            return false;
        }
        /// <summary>
        /// This method assumes that AutoReversePolylines has been called first
        /// And all polylines are oriented correctly with supply flow
        /// </summary>
        public Result CorrectPipesToCutLengths(Point3d connectionLocation)
        {
            if (psh == null) psh = new PropertySetHelper(ents?.FirstOrDefault()?.Database);

            Database localDb = ents.FirstOrDefault()?.Database;

            PipeSettingsCollection psc = PipeSettingsCollection.Load();

            PipesLengthCorrectionHandler plch;
            double curStart;
            double curEnd;
            Result result;

            // First from right to left
            curStart = 0;
            curEnd = this.GetStationAtPoint(connectionLocation);

            plch = new PipesLengthCorrectionHandler(
                this.GetEntitiesWithinStations(curStart, curEnd), true, psc);
            result = plch.CorrectLengths(localDb);

            // Then from left to right
            curStart = this.GetStationAtPoint(connectionLocation);
            curEnd = this.EndStation;

            plch = new PipesLengthCorrectionHandler(
                this.GetEntitiesWithinStations(curStart, curEnd), false, psc);
            result.Combine(plch.CorrectLengths(localDb));

            return result;
        }
        public double GetDistanceToPoint(Point3d pt, bool extend = false) =>
            GetClosestPointTo(pt, extend).DistanceHorizontalTo(pt);
        public IEnumerable<Polyline> GetPolylines() => ents.GetPolylines();
        public abstract Vector3d GetFirstDerivative(Point3d pt);
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
        public override Point3d StartPoint => al.StartPoint;
        public override Point3d EndPoint => al.EndPoint;
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
        public override double GetPolylineMiddleStation(Polyline pl) =>
            al.StationAtPoint(pl.GetPointAtDist(pl.Length / 2));
        public override double GetPolylineEndStation(Polyline pl) => al.StationAtPoint(pl.EndPoint);
        public override double GetBlockStation(BlockReference br) => al.StationAtPoint(br.Position);
        public override double GetStationAtPoint(Point3d pt) => al.StationAtPoint(pt);
        public override Point3d GetClosestPointTo(Point3d pt, bool extend = false)
        {
            Polyline plRef = null;
            try
            {
                plRef = this.al.GetPolyline().Go<Polyline>(this.al.Database.TransactionManager.TopTransaction);
                return plRef.GetClosestPointTo(pt, extend);
            }
            catch (Exception ex)
            {
                prdDbg(ex);
                throw;
            }
            finally
            {
                if (plRef != null) { plRef.UpgradeOpen(); plRef.Erase(true); }
            }
        }
        /// <summary>
        /// The entities are ordered by station (from start to end).
        /// Assuming start is always less than end.
        /// </summary>
        public override IEnumerable<Entity> GetEntitiesWithinStations(double start, double end)
        {
            return this.ents
                .Select(ent =>
                {
                    double station = double.NaN;
                    if (ent is Polyline pl)
                        station = al.StationAtPoint(pl.GetPointAtDist(pl.Length / 2));
                    else if (ent is BlockReference br)
                        station = al.StationAtPoint(br.Position);
                    return new { Entity = ent, Station = station };
                })
                .Where(x => x.Station >= start && x.Station <= end)
                .OrderBy(x => x.Station)
                .Select(x => x.Entity);
        }
        public override Point3d GetConnectionLocationToParent(IPipelineV2 parent, double tol)
        {
            //Assumptions:
            //This is connected to parent by endpoints -> ConnectionType: start or end
            //Parent is connected to this by start or end -> ConnectionType: middle
            //Cases:
            //1. Parent is connected S/E to this && this S/E is coincident with parent S/E -> end to end, start or end
            //2. Parent is not connected S/E to this && this S/E is connected to P -> afgrening
            //3. Parent is connected S/E to this && this S/E is not coincident with parent S/E -> middle

            //use a variable to cache the polyline reference
            //remember to erase it at the end
            Polyline parentPlRef = null;
            Polyline thisPlRef = null;
            try
            {
                switch (parent)
                {
                    case PipelineV2Alignment pal:
                        parentPlRef = pal.al.GetPolyline().Go<Polyline>(pal.al.Database.TransactionManager.TopTransaction);
                        thisPlRef = this.al.GetPolyline().Go<Polyline>(this.al.Database.TransactionManager.TopTransaction);

                        Point3d parentStart = pal.al.StartPoint;
                        Point3d parentEnd = pal.al.EndPoint;
                        Point3d thisStart = this.al.StartPoint;
                        Point3d thisEnd = this.al.EndPoint;

                        Point3d testPS;
                        Point3d testPE;

                        //Test for Case 1.
                        if (parentStart.DistanceHorizontalTo(thisStart) < tol ||
                            parentEnd.DistanceHorizontalTo(thisStart) < tol) return thisStart;
                        if (parentStart.DistanceHorizontalTo(thisEnd) < tol ||
                            parentEnd.DistanceHorizontalTo(thisEnd) < tol) return thisEnd;

                        //Test for Case 2.
                        testPS = parentPlRef.GetClosestPointTo(thisStart, false);
                        if (testPS.DistanceHorizontalTo(thisStart) < tol) return thisStart;
                        testPE = parentPlRef.GetClosestPointTo(thisEnd, false);
                        if (testPE.DistanceHorizontalTo(thisEnd) < tol) return thisEnd;

                        //Test for Case 3.
                        testPS = thisPlRef.GetClosestPointTo(parentStart, false);
                        if (testPS.DistanceHorizontalTo(parentStart) < tol) return testPS;
                        testPE = thisPlRef.GetClosestPointTo(parentEnd, false);
                        if (testPE.DistanceHorizontalTo(parentEnd) < tol) return testPE;

                        //If we get here, we have failed to find a connection location
                        throw new Exception($"Could not find connection location between {this.Name} and {parent.Name}!");
                    case PipelineV2Na pna:
                        throw new Exception($"Alignment pipeline {this.Name} cannot have NA {pna.Name} as parent!");
                    default:
                        throw new Exception($"Unknown pipeline type {parent.GetType()}!");
                }
            }
            catch (Exception ex)
            {
                prdDbg(ex);
                throw;
            }
            finally
            {
                if (parentPlRef != null)
                {
                    parentPlRef.UpgradeOpen();
                    parentPlRef.Erase(true);
                }
                if (thisPlRef != null)
                {
                    thisPlRef.UpgradeOpen();
                    thisPlRef.Erase(true);
                }
            }
        }
        public override Vector3d GetFirstDerivative(Point3d pt)
        {
            try
            {
                Point3d p = al.GetClosestPointTo(pt, false);
                return al.GetFirstDerivative(p);
            }
            catch (Exception ex)
            {
                prdDbg($"GetFirstDerivative(Point3d pt) failed for {al.Name} at point {pt}");
                prdDbg(ex);
                throw;
            }
        }
    }
    public class PipelineV2Na : PipelineV2Base
    {
        // This is a pipeline that does not belong to any alignment
        // All connected elements are grouped into this pipeline
        private Polyline topology;
        public PipelineV2Na(IEnumerable<Entity> source) : base(source)
        {
            #region Preparation to have stations for NA pipelines
            //Access ALL objects in database because we don't know our parent
            //Filter out the objects of the pipeline in question

            if (ents == null || ents.Count == 0)
                throw new Exception("PipelineV2Na cannot be created without entities!");

            Database db = ents.FirstOrDefault()?.Database;
            Transaction tx = db.TransactionManager.TopTransaction;
            var query =
                db.GetFjvEntities(tx, true, false)
                .Where(x => ents.All(y => x.Handle != y.Handle));

            Dictionary<Handle, Ent> allOtherEnts =
                query.ToDictionary(x => x.Handle, x => new Ent(x, psh));
            Dictionary<Handle, Ent> entites =
                ents.ToDictionary(x => x.Handle, x => new Ent(x, psh));

            var startingNodes = entites
                .SelectMany(ent => ent.Value.Cons
                .Where(con => allOtherEnts.ContainsKey(con.ConHandle))
                .Select(con => allOtherEnts[con.ConHandle]))
                .ToHashSet();

            Ent? foreignNode = null;

            foreach (var startingNode in startingNodes)
            {
                var resultNode = TraverseForValidNode(startingNode);
                if (resultNode != default)
                {
                    foreignNode = resultNode;
                    break;
                }
            }
            if (foreignNode == null)
                throw new Exception($"Could not find connecting node for pipeline {Name}!");

            Ent TraverseForValidNode(Ent startNode)
            {
                var stack = new Stack<Ent>();
                var visited = new HashSet<Ent>();
                stack.Push(startNode);

                Ent firstNode = startNode;

                while (stack.Count > 0)
                {
                    var currentNode = stack.Pop();

                    if (visited.Contains(currentNode))
                    {
                        continue;
                    }

                    visited.Add(currentNode);

                    // Read assigned alignment
                    string alignment = psh.Pipeline.ReadPropertyString(
                        currentNode.Entity, psh.PipelineDef.BelongsToAlignment);

                    // Check if the alignment is not "NA"
                    if (!alignment.StartsWith("NA", StringComparison.OrdinalIgnoreCase))
                    {
                        return currentNode; // Found a valid alignment, return the node
                    }

                    // Traverse through neighbors
                    foreach (var con in currentNode.Cons)
                    {
                        if (allOtherEnts.TryGetValue(con.ConHandle, out var neighbor) &&
                            !visited.Contains(neighbor)) { stack.Push(neighbor); }
                    }
                }

                //If the get here, no valid node was found
                return null;
            }

            //Determine entry point
            var temp = foreignNode.Cons.Where(x => entites.ContainsKey(x.ConHandle)).FirstOrDefault();
            if (temp == null)
                throw new Exception($"Could not find connecting node for pipeline {Name}!\n" +
                    $"Check connectivity with GRAPHWRITE.");

            Ent rootNode = entites[temp.ConHandle];

            Point3d startingPoint = rootNode.DetermineConnectionPoint(foreignNode);


            // now traverse the elements and build the topology polyline
            Stack<Entity> stack = new Stack<Entity>();
            stack.Push(con);

            Polyline polyline = new Polyline();



            while (stack.Count > 0)
            {
                var current = stack.Pop();

                switch (current)
                {
                    case Polyline pl:
                        if (pl.EndPoint.HorizontalEqualz(currentEntryPoint))
                        {
                            pl.UpgradeOpen();
                            pl.ReverseCurve();
                        }

                        //add the polyline to the topology

                        break;
                    case BlockReference br:
                        break;
                    default:
                        throw new System.Exception($"Polyline or BlockReference expected!");
                }
            }


            #endregion
        }
        public override string Name =>
            psh.Pipeline.ReadPropertyString(
                this.Entities.First(), psh.PipelineDef.BelongsToAlignment);
        public override double EndStation => topology.Length;
        public override Point3d StartPoint => topology.StartPoint;
        public override Point3d EndPoint => topology.EndPoint;
        public override double GetBlockStation(BlockReference br)
        {
            return 0;
        }
        public override double GetPolylineStartStation(Polyline pl)
        {
            return 0;
        }
        public override double GetPolylineMiddleStation(Polyline pl) =>
            GetStationAtPoint(pl.GetPointAtDist(pl.Length / 2));
        public override double GetPolylineEndStation(Polyline pl)
        {
            return 0;
        }
        public override double GetStationAtPoint(Point3d pt) => topology.GetDistAtPoint(pt);
        public override Point3d GetClosestPointTo(Point3d pt, bool extend = false) => topology.GetClosestPointTo(pt, extend);
        public override bool IsConnectedTo(IPipelineV2 other, double tol) =>
            this.Entities.IsConnectedTo(other.Entities);
        public override IEnumerable<Entity> GetEntitiesWithinStations(double start, double end)
        {
            throw new NotImplementedException("GetEntitiesWithinStations is not implemented for PipelineV2Na YET!!!");
        }
        public override Point3d GetConnectionLocationToParent(IPipelineV2 other, double tol)
        {
            throw new NotImplementedException($"GetConnectionLocatioToParent not implemented for {this.Name}!");
        }
        public override Vector3d GetFirstDerivative(Point3d pt) =>
            topology.GetFirstDerivative(topology.GetClosestPointTo(pt, false));
        private class Ent
        {
            public Entity Entity;
            public Handle Handle;
            public Con[] Cons;
            private Point3d[] EndPoints;
            public Ent(Entity entity, PropertySetHelper psh)
            {
                Entity = entity;
                Handle = entity.Handle;

                string conString = psh.Pipeline.ReadPropertyString(entity, psh.GraphDef.ConnectedEntities);
                if (conString.IsNoE())
                    throw new System.Exception(
                        $"Malformend constring: {conString}, entity: {entity.Handle}.");
                Cons = GraphEntity.parseConString(conString);

                switch (entity)
                {
                    case Polyline pl:
                        EndPoints = [pl.StartPoint, pl.EndPoint];
                        break;
                    case BlockReference br:
                        BlockTableRecord btr =
                            br.BlockTableRecord.Go<BlockTableRecord>(
                                br.Database.TransactionManager.TopTransaction);
                        HashSet<Point3d> collect = new();
                        foreach (Oid oid in btr)
                        {
                            if (!oid.IsDerivedFrom<BlockReference>()) continue;
                            BlockReference nestedBr = oid.Go<BlockReference>(
                                br.Database.TransactionManager.TopTransaction);
                            if (!nestedBr.Name.Contains("MuffeIntern")) continue;
                            Point3d wPt = nestedBr.Position;
                            wPt = wPt.TransformBy(br.BlockTransform);
                            collect.Add(wPt);
                        }
                        EndPoints = collect.ToArray();
                        break;
                    default:
                        throw new System.Exception($"Unknown entity type {entity.GetType()}!");
                }
            }
            /// <summary>
            /// Determines the connection point between this entity and another entity.
            /// Can only be used if the entites are proven connected by Cons.
            /// </summary>
            public Point3d DetermineConnectionPoint(Ent other)
            {
                Point3d result = Point3d.Origin;
                foreach (var pt in EndPoints)
                {
                    foreach (var otherPt in other.EndPoints)
                        if (pt.DistanceHorizontalTo(otherPt) < 0.001) return pt;
                }
                //If we get here, need to check connection to polyline
                //else there's a problem
                if (Entity is Polyline pl1 && other.Entity is Polyline pl2) return Point3d.Origin;
                else if (Entity is Polyline pl)
                {
                    var query = other.EndPoints
                        .Where(x => pl.GetClosestPointTo(x, false).DistanceHorizontalTo(x) < 0.001);
                    if (query.Any()) return query.First();
                }
                else if (other.Entity is Polyline pl3)
                {
                    var query = EndPoints
                        .Where(x => pl3.GetClosestPointTo(x, false).DistanceHorizontalTo(x) < 0.001);
                    if (query.Any()) return query.First();
                }
                return result;
            }
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
    public enum ConnectionType
    {
        Unknown,
        Start,
        End,
        Middle,
    }
}

