using Autodesk.AutoCAD.ApplicationServices;
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

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities.PipelineNetworkSystem
{
    public partial interface IPipelineV2
    {
        Graph<IPipelineSegmentV2> SegmentsGraph { get; }

        Result CorrectPipesToCutLengths(Point3d connectionLocation);

        void PopulateSegments(IPipelineV2? parent, IEnumerable<IPipelineV2> children);

    }
}
namespace IntersectUtilities.PipelineNetworkSystem
{
    public abstract partial class PipelineV2Base
    {
        public Graph<IPipelineSegmentV2> SegmentsGraph => _segmentsGraph;

        /// <summary>
        /// This method assumes that AutoReversePolylines has been called first
        /// And all polylines are oriented correctly with supply flow
        /// </summary>
        public Result CorrectPipesToCutLengths(Point3d connectionLocation)
        {
            Database? localDb = _pipelineEntities.FirstOrDefault()?.Database;
            if (localDb == null)
                throw new Exception($"Could not determine database for pipeline {this.Name}!");
            if (_psh == null)
                _psh = new PropertySetHelper(localDb);
            PipeSettingsCollection psc = PipeSettingsCollection.LoadWithValidation(localDb);

            PipesLengthCorrectionHandler plch;
            double curStart;
            double curEnd;
            Result result;

            // First from right to left
            curStart = 0;
            curEnd = this.GetStationAtPoint(connectionLocation);

            plch = new PipesLengthCorrectionHandler(
                this.GetEntitiesWithinStations(curStart, curEnd),
                true,
                psc
            );
            result = plch.CorrectLengths(localDb);

            // Then from left to right
            curStart = this.GetStationAtPoint(connectionLocation);
            curEnd = this.EndStation;

            plch = new PipesLengthCorrectionHandler(
                this.GetEntitiesWithinStations(curStart, curEnd),
                false,
                psc
            );
            result.Combine(plch.CorrectLengths(localDb));

            return result;
        }

        private Graph<IPipelineSegmentV2> _segmentsGraph;

        public void PopulateSegments(IPipelineV2? parent, IEnumerable<IPipelineV2> children)
        {
            if (_pipelineSizes == null) CreateSizeArray();
            if (_pipelineSizes == null) return;

            var sizeBrs = _pipelineEntities
                .Where(x => x is BlockReference)
                .Cast<BlockReference>()
                .Where(x => x.ReadDynamicCsvProperty(DynamicProperty.Function) == "SizeArray")
                .ToList();

            List<IPipelineSegmentV2> segs = new List<IPipelineSegmentV2>();
            //Create segments between transitions
            for (int i = 0; i < _pipelineSizes.Length; i++)
            {
                var curSize = _pipelineSizes[i];
                var ents = GetEntitiesWithinStations(curSize.StartStation, curSize.EndStation)
                    .Where(x => sizeBrs.All(y => y.Handle != x.Handle))
                    .ToList();
                if (ents.Count == 0) continue; //Skip empty segments (fx when reducer at end of pipeline)
                segs.Add(new PipelineSegmentV2(curSize, ents, this, _psh));
            }
            //Create segments for transitions
            foreach (var br in sizeBrs)
                segs.Add(new PipelineTransitionV2(GetStationAtPoint(br.Position), br, this, _psh));

            var orderedSegs = segs.OrderBy(x => x.MidStation).ToList();

            //Build the graph
            IPipelineSegmentV2 rootSegment;
            if (parent == null) //Parent = null means we are working with the first pipeline
            {
                if (_pipelineSizes.Length == 1) //Only one size, need to look at ends
                {
                    //If there's only one entity in the segment it doesn't matter if ends are connected
                    if (orderedSegs.Count() == 1) { rootSegment = orderedSegs.First(); }
                    else //Now check if ends are connected, to determine unconnected end
                    {
                        var firstSeg = orderedSegs.First();
                        bool firstConnected = children.Any(ch => IsConnectedToPipeline(firstSeg, ch));

                        var lastSeg = orderedSegs.Last();
                        bool lastConnected = children.Any(ch => IsConnectedToPipeline(lastSeg, ch));

                        bool IsConnectedToPipeline(IPipelineSegmentV2 seg, IPipelineV2 ppl)
                        {
                            return seg.Handles.Any(ppl.PipelineEntities.ExternalHandles.Contains);
                        }

                        if (firstConnected && !lastConnected) rootSegment = lastSeg;
                        else if (!firstConnected && lastConnected) rootSegment = firstSeg;
                        else
                        {
                            //Doesn't matter which segment is root here then
                            rootSegment = segs.First();
                        }
                    }
                }
                else
                {//Case 2: Choose by SIZE because multiple sizes
                    var maxSizeGroup = _pipelineSizes.Sizes
                        .GroupBy(x => x.DN)
                        .MaxBy(x => x.Key);

                    var qMax = maxSizeGroup.MaxBy(x => x.EndStation);
                    var qMin = maxSizeGroup.MinBy(x => x.StartStation);

                    if (qMax == _pipelineSizes.Sizes.MaxBy(x => x.EndStation))
                    {
                        rootSegment = orderedSegs.Last();
                    }
                    else if (qMin == _pipelineSizes.Sizes.MinBy(x => x.StartStation))
                    {
                        rootSegment = orderedSegs.First();
                    }
                    else
                    {
                        rootSegment = orderedSegs.Where(x =>
                        x.MidStation > qMax.StartStation &&
                        x.MidStation < qMax.EndStation).First();
                    }
                }
            }
            else
            {
                rootSegment = GetSegmentConnectedToParent(parent, segs);
            }

            var nodes = orderedSegs.Select(seg => new Node<IPipelineSegmentV2>(seg)).ToList();
            var rootIndex = orderedSegs.IndexOf(rootSegment);
            if (rootIndex < 0) rootIndex = 0;
            var rootNode = nodes[rootIndex];
            if (rootIndex == 0)
            {
                for (int i = 1; i < nodes.Count; i++)
                {
                    nodes[i - 1].AddChild(nodes[i]);
                }
            }
            else if (rootIndex == nodes.Count - 1)
            {
                for (int i = rootIndex - 1; i >= 0; i--)
                {
                    nodes[i + 1].AddChild(nodes[i]);
                }
            }
            else
            {
                for (int i = rootIndex - 1; i >= 0; i--)
                {
                    nodes[i + 1].AddChild(nodes[i]);
                }
                for (int i = rootIndex + 1; i < nodes.Count; i++)
                {
                    nodes[i - 1].AddChild(nodes[i]);
                }
            }
            _segmentsGraph = new Graph<IPipelineSegmentV2>(
                rootNode,
                seg => $"{seg.Owner.Name}-{seg.MidStation:F3}",
                seg => $"{seg.Label}"
            );

            if (parent != null)
                ConnectChildToParent(parent, rootNode);
        }

        private IPipelineSegmentV2 GetSegmentConnectedToParent(
            IPipelineV2 parent,
            IReadOnlyList<IPipelineSegmentV2> segments)
        {
            var query = segments.Where(
                x => x.Handles.Any(parent.PipelineEntities.ExternalHandles.Contains));

            var count = query.Count();
            if (count == 0)
                throw new Exception(
                    $"Could NOT FIND segment in pipeline {this.Name} that connects to parent {parent.Name}!");
            if (count > 1)
                throw new Exception(
                    $"Found MULTIPLE segments in pipeline {this.Name} that connects to parent {parent.Name}!");

            return query.First();
        }

        private void ConnectChildToParent(IPipelineV2 parent, Node<IPipelineSegmentV2> child)
        {
            var query = parent.SegmentsGraph.Dfs()
                .Where(x => x.Value.IsConnectedTo(child.Value));

            var parentSeg = query.FirstOrDefault();
            if (parentSeg == null)
                throw new Exception(
                    $"Could NOT FIND segment in pipeline {parent.Name} " +
                    $"that connects to child segment at {child.Value.MidStation:F3} " +
                    $"in pipeline {this.Name}!");

            parentSeg.AddChild(child);
        }

    }
}
namespace IntersectUtilities.PipelineNetworkSystem
{
    public static partial class PipelineV2Factory
    {
        public static IPipelineV2 Create(IEnumerable<Entity> ents, Alignment al)
        {
            PropertySetHelper psh = PropertySetsOf(ents);
            if (al == null)
                return new PipelineV2Na(ents, psh);
            else
                return new PipelineV2Alignment(ents, al, psh);
        }

        private static PropertySetHelper PropertySetsOf(IEnumerable<Entity> ents)
        {
            try
            {
                return new PropertySetHelper(ents?.FirstOrDefault()?.Database);
            }
            catch (Exception)
            {
                if (ents == null)
                    prdDbg(@"pipelineEntities is null!");
                else
                    foreach (var entity in ents)
                    {
                        prdDbg(entity.Handle);
                    }
                throw;
            }
        }

    }
}
namespace IntersectUtilities.PipelineNetworkSystem
{

    public class PipelineV2Alignment : PipelineV2Base
    {
        internal Alignment al;

        public PipelineV2Alignment(IEnumerable<Entity> ents, Alignment al, PropertySetHelper psh)
            : base(ents, psh)
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

        public override double GetPolylineStartStation(Polyline pl) =>
            al.StationAtPoint(pl.StartPoint);

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
                plRef = this
                    .al.GetPolyline()
                    .Go<Polyline>(this.al.Database.TransactionManager.TopTransaction);
                return plRef.GetClosestPointTo(pt, extend);
            }
            catch (Exception ex)
            {
                prdDbg(ex);
                throw;
            }
            finally
            {
                if (plRef != null)
                {
                    plRef.UpgradeOpen();
                    plRef.Erase(true);
                }
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

            return this
                ._pipelineEntities.Select(ent =>
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
            try
            {
                if (parent is PipelineV2Na pna)
                    throw new Exception(
                        $"Alignment pipeline {this.Name} cannot have NA {pna.Name} as parent!"
                    );
                if (TryGetConnectionLocationToParent(parent, tol, out Point3d location))
                    return location;

                //If we get here, we have failed to find a connection location
                throw new Exception(
                    $"Could not find connection location between {this.Name} and {parent.Name}!"
                );
            }
            catch (Exception ex)
            {
                prdDbg(ex);
                throw;
            }
        }

        /// <summary>
        /// Nothing to repair: an alignment pipeline meets its (alignment) parent
        /// on the parent's path itself, never through one of its parts.
        /// </summary>
        public override void RepairBranchReferenceToParent(IPipelineV2 parent, double tol) { }

        public override bool TryGetConnectionLocationToParent(
            IPipelineV2 parent, double tol, out Point3d location)
        {
            //Assumptions:
            //This is connected to parent by endpoints -> ConnectionType: start or end
            //Parent is connected to this by start or end -> ConnectionType: middle
            //Cases:
            //1. Parent is connected S/E to this && this S/E is coincident with parent S/E -> end to end, start or end
            //2. Parent is not connected S/E to this && this S/E is connected to P -> afgrening
            //3. Parent is connected S/E to this && this S/E is not coincident with parent S/E -> middle
            location = Point3d.Origin;
            switch (parent)
            {
                case PipelineV2Alignment:
                    break;
                case PipelineV2Na:
                    //An alignment pipeline never has an NA parent.
                    return false;
                default:
                    throw new Exception($"Unknown pipeline type {parent.GetType()}!");
            }
            PipelineV2Alignment pal = (PipelineV2Alignment)parent;

            //use a variable to cache the polyline reference
            //remember to erase it at the end
            Polyline parentPlRef = null;
            Polyline thisPlRef = null;
            try
            {
                parentPlRef = pal
                    .al.GetPolyline()
                    .Go<Polyline>(pal.al.Database.TransactionManager.TopTransaction);
                thisPlRef = this
                    .al.GetPolyline()
                    .Go<Polyline>(this.al.Database.TransactionManager.TopTransaction);

                Point3d parentStart = pal.al.StartPoint;
                Point3d parentEnd = pal.al.EndPoint;
                Point3d thisStart = this.al.StartPoint;
                Point3d thisEnd = this.al.EndPoint;

                Point3d testPS;
                Point3d testPE;

                //Test for Case 1.
                if (
                    parentStart.DistanceHorizontalTo(thisStart) < tol
                    || parentEnd.DistanceHorizontalTo(thisStart) < tol
                )
                {
                    location = thisStart;
                    return true;
                }
                if (
                    parentStart.DistanceHorizontalTo(thisEnd) < tol
                    || parentEnd.DistanceHorizontalTo(thisEnd) < tol
                )
                {
                    location = thisEnd;
                    return true;
                }

                //Test for Case 2.
                testPS = parentPlRef.GetClosestPointTo(thisStart, false);
                if (testPS.DistanceHorizontalTo(thisStart) < tol)
                {
                    location = thisStart;
                    return true;
                }
                testPE = parentPlRef.GetClosestPointTo(thisEnd, false);
                if (testPE.DistanceHorizontalTo(thisEnd) < tol)
                {
                    location = thisEnd;
                    return true;
                }

                //Test for Case 3.
                testPS = thisPlRef.GetClosestPointTo(parentStart, false);
                if (testPS.DistanceHorizontalTo(parentStart) < tol)
                {
                    location = testPS;
                    return true;
                }
                testPE = thisPlRef.GetClosestPointTo(parentEnd, false);
                if (testPE.DistanceHorizontalTo(parentEnd) < tol)
                {
                    location = testPE;
                    return true;
                }

                return false;
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
                    $"al.GetFirstDerivative(Point3d pt) failed for {al.Name} at point {pt}\n"
                        + $"with closest point being {p}."
                );
                prdDbg($"PipelineV2.cs line 389");

                Polyline pl = null;
                try
                {
                    prdDbg($"Trying with a polyline ->");

                    pl = al.GetPolyline()
                        .Go<Polyline>(al.Database.TransactionManager.TopTransaction);
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
                    if (pl != null)
                    {
                        pl.UpgradeOpen();
                        pl.Erase(true);
                    }
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
                    opl.GetEndWidthAt(i)
                );
            }

            opl.CheckOrOpenForWrite();
            opl.Erase(true);
            return pl;
        }
    }

}
