using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.Collections;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;

using MoreLinq;

using System;
using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.UtilsCommon.Utils;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities.PipelineNetworkSystem
{
    public interface IPipelineV2
    {
        string Name { get; }
        string Label { get; }
        double EndStation { get; }
        Point3d StartPoint { get; }
        Point3d EndPoint { get; }
        EntityCollection PipelineEntities { get; }
        EntityCollection PipelineWelds { get; }
        IPipelineSizeArrayV2 PipelineSizes { get; }
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
        /// <summary>
        /// Returns the pipelines' topology as polyline with
        /// start point coinciding with start station.
        /// </summary>
        Polyline GetTopologyPolyline();
        void PopulateSegments(IPipelineV2? parent);
    }
    public abstract class PipelineV2Base : IPipelineV2
    {
        protected EntityCollection _pipelineEntities;
        protected EntityCollection _pipelineWelds;
        protected IPipelineSizeArrayV2 _pipelineSizes;
        protected PropertySetHelper _psh;
        public EntityCollection PipelineEntities { get => _pipelineEntities; }
        public EntityCollection PipelineWelds { get => _pipelineWelds; }
        public abstract string Name { get; }
        public virtual string Label { get => $"\"{Name}\""; }
        public abstract double EndStation { get; }
        public IPipelineSizeArrayV2 PipelineSizes => _pipelineSizes;
        public abstract Point3d StartPoint { get; }
        public abstract Point3d EndPoint { get; }
        public PipelineV2Base(IEnumerable<Entity> source)
        {
            source.Partition(IsNotWeld, out this._pipelineEntities, out this._pipelineWelds);

            try
            {

                if (_psh == null)
                    _psh = new PropertySetHelper(_pipelineEntities?.FirstOrDefault()?.Database);
            }
            catch (Exception)
            {
                if (_pipelineEntities == null)
                    prdDbg(@"pipelineEntities is null!");
                else foreach (var entity in _pipelineEntities) { prdDbg(entity.Handle); }
                throw;
            }

            bool IsNotWeld(Entity e) =>
                (e is BlockReference br &&
                br.ReadDynamicCsvProperty(
                    DynamicProperty.Type, false) != "Svejsning") ||
                    e is Polyline; //<-- this is the culprit!!!!!!!!!!!!!!!!!!!!
        }
        public int GetMaxDN() => _pipelineEntities.GetMaxDN();
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
            _pipelineSizes = PipelineSizeArrayFactory.CreateSizeArray(this);
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
            if (_pipelineSizes == null) CreateSizeArray();

            //Case one size -> return start point
            if (_pipelineSizes.Length == 1) return this.StartPoint;

            if (_pipelineSizes.Sizes.First().System == PipeSystemEnum.Stål &&
                _pipelineSizes.Sizes.Last().System == PipeSystemEnum.Stål)
            {
                if (_pipelineSizes.Sizes.First().DN > _pipelineSizes.Sizes.Last().DN) return this.StartPoint;
                else return this.EndPoint;
            }
            else if (_pipelineSizes.Sizes.First().System == PipeSystemEnum.Stål) return this.StartPoint;
            else if (_pipelineSizes.Sizes.Last().System == PipeSystemEnum.Stål) return this.EndPoint;
            else
            {
                if (_pipelineSizes.Sizes.First().DN > _pipelineSizes.Sizes.Last().DN) return this.StartPoint;
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
            Database? localDb = _pipelineEntities.FirstOrDefault()?.Database;
            if (localDb == null) throw new Exception($"Could not determine database for pipeline {this.Name}!");
            if (_psh == null) _psh = new PropertySetHelper(localDb);
            PipeSettingsCollection psc = PipeSettingsCollection.LoadWithValidation(localDb);

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
        public IEnumerable<Polyline> GetPolylines() => _pipelineEntities.GetPolylines();
        public abstract Vector3d GetFirstDerivative(Point3d pt);
        public abstract Polyline GetTopologyPolyline();
        private Graph<IPipelineSegmentV2> _segmentsGraph;
        public void PopulateSegments(IPipelineV2? parent)
        {
            if (_pipelineSizes == null) CreateSizeArray();
            if (_pipelineSizes == null) return;

            var sizeBrs = _pipelineEntities
                .Where(x => x is BlockReference)
                .Cast<BlockReference>()
                .Where(x => x.ReadDynamicCsvProperty(
                    DynamicProperty.Function) == "SizeArray")
                .ToList();

            var entGroups = new List<(double station, List<Entity> ents)>();
            for (int i = 0; i < _pipelineSizes.Length; i++)
            {
                var curSize = _pipelineSizes[i];
                var ents = GetEntitiesWithinStations(
                    curSize.StartStation, curSize.EndStation)
                    .Where(x => sizeBrs.All(y => y.Handle != x.Handle))
                    .ToList();
                entGroups.Add(((curSize.StartStation + curSize.EndStation) / 2, ents));
            }
            foreach (var br in sizeBrs) entGroups.Add(
                (GetStationAtPoint(br.Position), [br]));

            var orderedGroups = entGroups.OrderBy(x => x.station);

            List<IPipelineSegmentV2> segs = new List<IPipelineSegmentV2>();
            foreach (var part in orderedGroups)
                PipelineSegmentFactoryV2.Create(part);
        }
    }
    public class PipelineV2Alignment : PipelineV2Base
    {
        internal Alignment al;
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
                    return this.PipelineEntities.IsConnectedTo(pna.PipelineEntities);
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
        /// Stations are auto normalized, ie. if start > end, they are swapped.
        /// </summary>
        public override IEnumerable<Entity> GetEntitiesWithinStations(double start, double end)
        {
            if (start > end)
            {
                var temp = end;
                end = start;
                start = temp;
            }

            return this._pipelineEntities
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
            Point3d p = default;

            try
            {
                p = al.GetClosestPointTo(pt, false);
                return al.GetFirstDerivative(p);
            }
            catch (Exception ex)
            {
                prdDbg(
                    $"al.GetFirstDerivative(Point3d pt) failed for {al.Name} at point {pt}\n" +
                    $"with closest point being {p}.");
                prdDbg($"PipelineV2.cs line 389");

                Polyline pl = null;
                try
                {
                    prdDbg($"Trying with a polyline ->");

                    pl = al.GetPolyline().Go<Polyline>(al.Database.TransactionManager.TopTransaction);
                    p = pl.GetClosestPointTo(pt, false);
                    var dir = pl.GetFirstDerivative(p);
                    prdDbg("Success with polyline!");
                    return dir;
                }
                catch (Exception)
                {
                    prdDbg("FAILURE! Polyline failed also!");
                    throw;
                }
                finally
                {
                    if (pl != null) { pl.UpgradeOpen(); pl.Erase(true); }
                }

                throw;
            }
        }
        public override Polyline GetTopologyPolyline()
        {
            var opl = UtilsCommon.Extensions.GetPolyline(al);
            var pl = new Polyline(opl.NumberOfVertices);

            for (int i = 0; i < opl.NumberOfVertices; i++)
            {
                pl.AddVertexAt(
                    i,
                    opl.GetPoint2dAt(i),
                    opl.GetBulgeAt(i),
                    opl.GetStartWidthAt(i),
                    opl.GetEndWidthAt(i));
            }

            opl.CheckOrOpenForWrite();
            opl.Erase(true);
            return pl;
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

            if (_pipelineEntities == null || _pipelineEntities.Count == 0)
                throw new Exception("PipelineV2Na cannot be created without entities!");

            //prdDbg($"Creating NA pipeline {Name}...");

            Database db = _pipelineEntities.FirstOrDefault()?.Database;
            Transaction tx = db.TransactionManager.TopTransaction;
            var query =
                db.GetFjvEntities(tx, true, false)
                .Where(x => _pipelineEntities.All(y => x.Handle != y.Handle));

            Dictionary<Handle, Ent> externalEntities =
                query.ToDictionary(x => x.Handle, x => new Ent(x, _psh));
            Dictionary<Handle, Ent> localEntities =
                _pipelineEntities.ToDictionary(x => x.Handle, x => new Ent(x, _psh));

            var connectedExternalEntities = localEntities
                .SelectMany(ent => ent.Value.Cons
                .Where(con => externalEntities.ContainsKey(con.ConHandle))
                .Select(con => externalEntities[con.ConHandle]))
                .ToHashSet();

            //Here we find the first non-NA node that connects to the pipeline.
            //It can be nested in a higher level NA pipeline as we can
            //have multiple levels of NA pipelines.
            //This is to find the correct entry point for the pipeline
            //and thus determine direction of the pipeline.
            //Also assuming that there's only one non-NA node that connects the sub-tree
            //to the main network.
            Ent? nonNaNode = null;
            Ent? externalRootNode = null;
            foreach (var connectedExternalEntity in connectedExternalEntities)
            {
                var resultNode = TraverseForValidNode(connectedExternalEntity);
                if (resultNode != default)
                {
                    nonNaNode = resultNode;
                    externalRootNode = connectedExternalEntity;
                    break;
                }
            }
            if (nonNaNode == null || externalRootNode == null)
                throw new Exception($"Could not find connecting node for pipeline {Name}!");

            Ent TraverseForValidNode(Ent connectedExternalEntity)
            {
                var stack = new Stack<Ent>();
                var visited = new HashSet<Ent>();
                stack.Push(connectedExternalEntity);

                Ent firstNode = connectedExternalEntity;

                while (stack.Count > 0)
                {
                    var currentNode = stack.Pop();

                    if (visited.Contains(currentNode))
                    {
                        continue;
                    }

                    visited.Add(currentNode);

                    // Read assigned alignment
                    string alignment = _psh.Pipeline.ReadPropertyString(
                        currentNode.Entity, _psh.PipelineDef.BelongsToAlignment);

                    // Check if the alignment is not "NA"
                    if (!alignment.StartsWith("NA", StringComparison.OrdinalIgnoreCase))
                    {
                        return currentNode; // Found a valid alignment, return the node
                    }

                    // Traverse through neighbors
                    foreach (var con in currentNode.Cons)
                    {
                        if (externalEntities.TryGetValue(con.ConHandle, out var neighbor) &&
                            !visited.Contains(neighbor)) { stack.Push(neighbor); }
                    }
                }

                //If the get here, no valid node was found
                return null;
            }

            //Determine the local entry node
            var temp = externalRootNode.Cons.Where(x => localEntities.ContainsKey(x.ConHandle)).FirstOrDefault();
            if (temp == null)
                throw new Exception($"Could not find connecting node for pipeline {Name}!\n" +
                    $"Check connectivity with GRAPHWRITE.");

            Ent localRootNode = localEntities[temp.ConHandle];

            Point3d startingPoint = localRootNode.DetermineConnectionPoint(externalRootNode);

            // now traverse the elements and build the topology polyline
            Stack<Ent> stack = new Stack<Ent>();
            stack.Push(localRootNode);

            Polyline topology = new Polyline();

            HashSet<Ent> visited = new HashSet<Ent>();
            while (stack.Count > 0)
            {
                Ent current = stack.Pop();
                visited.Add(current);

                switch (current.Entity)
                {
                    case BlockReference br:
                        topology.AddVertexAt(topology.NumberOfVertices, startingPoint.To2d(), 0, 0, 0);
                        topology.AddVertexAt(topology.NumberOfVertices, br.Position.To2d(), 0, 0, 0);
                        //Determine the next connection point
                        //Case 1: all ents are visited
                        //And so we cannot decisively determine the next connection point
                        //So we take the first not current end point
                        if (visited.Count == localEntities.Count)
                        {
                            var endPoints = br.GetAllEndPoints();
                            //For example an endbund.
                            if (endPoints.Count == 1)
                                throw new NotImplementedException(
                                    $"Entity {br.Handle} has only one endpoint!\n" +
                                    $"And is located in a NA pipeline!\n" +
                                    $"This is not supported yet!");
                            else
                            {
                                var pquery = endPoints.Where(x => x.DistanceHorizontalTo(startingPoint) > 0.001);
                                if (pquery.Any())
                                    topology.AddVertexAt(
                                        topology.NumberOfVertices, pquery.First().To2d(), 0, 0, 0);
                                else throw new Exception(
                                    $"Could not determine next connection point for {br.Handle}!");
                            }
                        }
                        //Case 2: there are still unvisited entities
                        //And so we determine the next connection point by the next entity
                        else
                        {
                            var nquery = localEntities
                                .Where(x => !visited.Contains(x.Value))
                                .Select(x => x.Value)
                                .Where(x => x.Cons.Any(y => y.ConHandle == br.Handle))
                                .FirstOrDefault();

                            if (nquery == null)
                                throw new Exception(
                                    $"Could not determine next connection point for {br.Handle}!");

                            //Feed the starting point for the next entity to the next loop
                            startingPoint = nquery.DetermineConnectionPoint(current);
                            //Assure continuation of traversal by pushing the next entity to the stack
                            stack.Push(nquery);
                        }
                        break;
                    case Polyline pl:
                        {//Test to see if the polyline is oriented correctly
                            bool reversed = false;
                            if (pl.StartPoint.DistanceHorizontalTo(startingPoint) > 0.001)
                            {
                                pl.CheckOrOpenForWrite();
                                pl.ReverseCurve();
                                reversed = true;
                            }
                            //Add the polyline to the topology
                            //But skip the last point as it will be added by the next entity
                            for (int i = 0; i < pl.NumberOfVertices - 1; i++)
                            {
                                Point2d pt = pl.GetPoint2dAt(i);
                                topology.AddVertexAt(topology.NumberOfVertices, pt,
                                    pl.GetBulgeAt(i), 0, 0);
                            }
                            //Feed the starting point for the next entity to the next loop
                            startingPoint = pl.EndPoint;

                            //Determine the next node
                            var nquery = localEntities
                                    .Where(x => !visited.Contains(x.Value))
                                    .Select(x => x.Value)
                                    .Where(x => x.Cons.Any(y => y.ConHandle == pl.Handle))
                                    .FirstOrDefault();

                            if (nquery == null)
                            {
                                //In this case we are looking at last element
                                //And so we need to add the last point
                                topology.AddVertexAt(topology.NumberOfVertices, startingPoint.To2d(), 0, 0, 0);
                                //the loop will exit now because we don't push next entity to the stack
                            }
                            else
                            {
                                //Assure continuation of traversal by pushing the next entity to the stack
                                stack.Push(nquery);
                            }
                            if (reversed) pl.ReverseCurve();
                        }
                        break;
                    default:
                        throw new Exception(
                            $"Unknown entity type in NA pipeline " +
                            $"topology building {current.Entity.GetType()}!");
                }
            }

            //Cache the topology
            this.topology = topology;
            #endregion
        }
        public override string Name =>
            _psh.Pipeline.ReadPropertyString(
                this.PipelineEntities.First(), _psh.PipelineDef.BelongsToAlignment);
        public override double EndStation => topology.Length;
        public override Point3d StartPoint => topology.StartPoint;
        public override Point3d EndPoint => topology.EndPoint;
        public override double GetBlockStation(BlockReference br) =>
            GetStationAtPoint(GetClosestPointTo(br.Position, false));
        public override double GetPolylineStartStation(Polyline pl) =>
            GetStationAtPoint(GetClosestPointTo(pl.StartPoint, false));
        public override double GetPolylineMiddleStation(Polyline pl) =>
            GetStationAtPoint(pl.GetPointAtDist(pl.Length / 2));
        public override double GetPolylineEndStation(Polyline pl) =>
            GetStationAtPoint(GetClosestPointTo(pl.EndPoint, false));
        public override double GetStationAtPoint(Point3d pt) => topology.GetDistAtPoint(topology.GetClosestPointTo(pt, false));
        public override Point3d GetClosestPointTo(Point3d pt, bool extend = false) => topology.GetClosestPointTo(pt, extend);
        public override bool IsConnectedTo(IPipelineV2 other, double tol) =>
            this.PipelineEntities.IsConnectedTo(other.PipelineEntities);
        public override IEnumerable<Entity> GetEntitiesWithinStations(double start, double end)
        {
            return this._pipelineEntities
                .Select(ent =>
                {
                    double station = double.NaN;
                    if (ent is Polyline pl)
                        station = GetStationAtPoint(pl.GetPointAtDist(pl.Length / 2));
                    else if (ent is BlockReference br)
                        station = GetStationAtPoint(br.Position);
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

            //4. Case where the NA is connected to a tee like and does not reach the alignment geometry
            //   In this case we need to find the connection point to the tee like using geometry of components

            //use a variable to cache the polyline reference
            //remember to erase it at the end
            Polyline parentPlRef = null;
            Polyline thisPlRef = this.topology;
            try
            {
                switch (parent)
                {
                    case PipelineV2Alignment pal:
                        parentPlRef = pal.al.GetPolyline().Go<Polyline>(pal.al.Database.TransactionManager.TopTransaction);
                        break;
                    case PipelineV2Na pna:
                        parentPlRef = pna.topology;
                        break;
                    default:
                        throw new Exception($"Unknown pipeline type {parent.GetType()}!");
                }

                Point3d parentStart = parentPlRef.StartPoint;
                Point3d parentEnd = parentPlRef.EndPoint;
                Point3d thisStart = thisPlRef.StartPoint;
                Point3d thisEnd = thisPlRef.EndPoint;

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

                //Test for Case 4.
                Point3d con = Point3d.Origin;
                var ent1 = parent.PipelineEntities.GetEntityByPoint(thisStart);
                var ent2 = parent.PipelineEntities.GetEntityByPoint(thisEnd);
                var ent = ent1 ?? ent2;

                if (ent1 != null) con = parentPlRef.GetClosestPointTo(thisStart, false);
                else if (ent2 != null) con = parentPlRef.GetClosestPointTo(thisEnd, false);

                if (ent1 == null && ent2 == null) throw new Exception(
                    $"Could not find connection location between {this.Name} and {parent.Name}!");
                else
                {
                    //Dirty fix for missing branch connection references
                    HashSet<string> names = [
                        "Afgrening med spring",
                        "Lige afgrening",
                        "Muffetee",
                        "Parallelafgrening",
                        "Preskobling tee",
                    ];
                    if (ent is BlockReference br && names.Contains(br.ReadDynamicCsvProperty(DynamicProperty.Type)))
                    {
                        _psh.Pipeline.WritePropertyString(ent, _psh.PipelineDef.BranchesOffToAlignment, this.Name);
                    }

                    return con;
                }



                //If we get here, we have failed to find a connection location
                throw new Exception($"Could not find connection location between {this.Name} and {parent.Name}!");
            }
            catch (Exception ex)
            {
                prdDbg(ex);
                throw;
            }
            finally
            {
                if (parentPlRef != null && parent is PipelineV2Alignment)
                {
                    parentPlRef.UpgradeOpen();
                    parentPlRef.Erase(true);
                }
            }
        }
        public override Vector3d GetFirstDerivative(Point3d pt) =>
            topology.GetFirstDerivative(GetClosestPointTo(pt, false));
        public override Polyline GetTopologyPolyline() => (Polyline)topology.Clone();
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

                string conString = psh.Graph.ReadPropertyString(entity, psh.GraphDef.ConnectedEntities);
                if (conString.IsNoE())
                    throw new System.Exception(
                        $"Malformend constring: {conString}, entity: {entity.Handle}.");
                Cons = Con.ParseConString(conString);

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