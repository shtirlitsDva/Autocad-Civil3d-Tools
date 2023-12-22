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
        double GetPolylineEndStation(Polyline pl);
        double GetBlockStation(BlockReference br);
        double GetStationAtPoint(Point3d pt);
        Point3d GetClosestPointTo(Point3d pt, bool extend = false);
        bool IsConnectedTo(IPipelineV2 other, double tol);
        Point3d GetConnectionLocationToParent(IPipelineV2 other, double tol);
        bool DetermineUnconnectedEndPoint(IPipelineV2 other, double tol, out Point3d freeEnd);
        void AutoReversePolylines(Point3d connectionLocation);
        IEnumerable<Entity> GetEntitiesWithinStations(double start, double end);
        Point3d GetLocationForMaxDN();
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
                            Line l = new Line(Point3d.Origin, pl.GetPointAtDist(pl.Length / 2));
                            l.AddEntityToDbModelSpace(Application.DocumentManager.MdiActiveDocument.Database);
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
                            Line l = new Line(Point3d.Origin, pl.GetPointAtDist(pl.Length / 2));
                            l.AddEntityToDbModelSpace(Application.DocumentManager.MdiActiveDocument.Database);
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
