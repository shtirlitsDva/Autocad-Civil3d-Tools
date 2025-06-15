using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.NTS;

using NetTopologySuite.Geometries;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Exception = System.Exception;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Dreambuild.AutoCAD;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal class AP_PipelineData
    {
        public AP_PipelineData(string name)
        {
            Name = name;
        }
        [JsonInclude]
        public string Name { get; set; }
        [JsonInclude]
        public AP_SurfaceProfileData? SurfaceProfile { get; set; } = null;
        [JsonIgnore]
        public IPipelineSizeArrayV2? SizeArray { get; set; } = null;
        [JsonIgnore]
        public List<AP_HorizontalArc> HorizontalArcs { get; set; } = new();
        [JsonIgnore]
        public AP_ProfileViewData? ProfileView { get; set; } = null;
        [JsonInclude]
        public List<AP_Utility> Utility { get; set; } = new();
        public void GenerateAvoidanceGeometryForUtilities()
        {
            foreach (var utility in Utility)
            {
                //Avoidance arc
                double radius = SizeArray!.GetSizeAtStation(utility.MidStation).VerticalMinRadius;
                utility.BuildExtendedAvoidanceArc(radius);

                //Harc avoidance polyline
                //Find the horizontal arcs that cover this utility
                var harcsForThisUtility = HorizontalArcs
                    .Where(harc => harc.StartStation <= utility.EndStation &&
                                   harc.EndStation >= utility.StartStation)
                    .ToList();
                if (harcsForThisUtility.Count == 0) continue;
                double startStation = harcsForThisUtility.Min(x => x.StartStation);
                double endStation = harcsForThisUtility.Max(x => x.EndStation);
                utility.BuildHorizontalArcPolylineWithTrimmedArcs(startStation, endStation);
            }
        }
        internal void GenerateAvoidancePolygonsForUtilities()
        {
            foreach (var utility in Utility)
            {
                if (utility.AvoidanceArc == null) continue;

                #region Normal avoidance region
                Polygon polygon = NTSConversion.ConvertClosedPlineToNTSPolygonWithCurveApproximation(
                    utility.AvoidanceArc);
                if (polygon == null) throw new Exception($"No polygon created for utility!");
                utility.AvoidancePolygon = polygon;
                #endregion

                #region Harc avoidance region
                if (utility.HorizontalArcAvoidancePolyline == null) continue;

                Polygon poly = NTSConversion.ConvertClosedPlineToNTSPolygonWithCurveApproximation(
                    utility.HorizontalArcAvoidancePolyline);

                if (poly == null) throw new Exception($"No polygon created for utility!");

                utility.HorizontalArcAvoidancePolygon = poly;
                #endregion
            }
        }
        internal void MergeAvoidancePolygonsForUtilities()
        {
            //Merge the avoidance polygons for each utility
            foreach (AP_Utility utility in Utility)
            {
                if (utility.AvoidancePolygon == null) continue;
                if (utility.HorizontalArcAvoidancePolygon == null)
                {
                    utility.MergedAvoidancePolygon = utility.AvoidancePolygon;
                    continue;
                }
                var merged = utility.AvoidancePolygon.Union(utility.HorizontalArcAvoidancePolygon);
                if (merged is Polygon poly)
                {
                    utility.MergedAvoidancePolygon = poly;
                }
                else throw new Exception($"Merged polygon is not a valid polygon!");
            }
        }
        public Polyline? UnfilletedPolyline { get; set; }
        public Polyline? FilletedPolyline { get; private set; } // To store the result

        internal void ProcessSelectedUtilitiesToCreateUnfilletedPolyline()
        {
            if (SurfaceProfile == null) throw new Exception("No surface profile found for the pipeline!");
            if (SurfaceProfile.OffsetCentrelines == null) throw new Exception("No offset centrelines found for the surface profile!");
            if (ProfileView == null) throw new Exception("No profile view found for the pipeline!");
            var pv = ProfileView.ProfileView;

            Region? mergedRegion = null;

            //1. Create a polygon from pipe centreline
            var poly = SurfaceProfile.OffsetCentrelines.Clone() as Polyline;
            if (poly == null) throw new Exception("No offset centrelines found for the surface profile!");
            var sp = poly.GetPoint2dAt(0);
            poly.AddVertexAt(0, new Point2d(sp.X, sp.Y + 20), 0, 0, 0);
            var ep = poly.GetPoint2dAt(poly.NumberOfVertices - 1);
            poly.AddVertexAt(poly.NumberOfVertices, new Point2d(ep.X, ep.Y + 20), 0, 0, 0);
            poly.Closed = true;
            DBObjectCollection regions = Region.CreateFromCurves(new DBObjectCollection { poly });
            if (regions.Count == 0) throw new Exception("No regions created from the offset centrelines!");
            Region mainRegion = regions[0] as Region;
            if (mainRegion == null) throw new Exception("No main region created from the offset centrelines!");

            //2. Merge pipe centreline polygon with selected utility polygons
            var query = Utility.Where(
                x => x.Status == AP_Status.Selected);

            if (query.Count() == 0) { mergedRegion = mainRegion; } //No selected utilities
            else
            {
                foreach (var utility in query)
                {
                    mainRegion.BooleanOperation(BooleanOperationType.BoolUnite, utility.AvoidanceRegion!);

                    if (utility.HorizontalArcAvoidanceRegion != null)
                    {
                        mainRegion.BooleanOperation(BooleanOperationType.BoolUnite, utility.HorizontalArcAvoidanceRegion);
                    }
                }

                //Trim
                var bbox = mainRegion.GeometricExtents;
                var origo = ProfileView.ProfileView.Location;

                //Left side
                Polyline polyline = new Polyline(4);
                polyline.AddVertexAt(0, origo.To2d(), 0, 0, 0);
                polyline.AddVertexAt(1, new Point2d(origo.X, bbox.MaxPoint.Y + 1), 0, 0, 0);
                polyline.AddVertexAt(2, new Point2d(bbox.MinPoint.X - 1, bbox.MaxPoint.Y + 1), 0, 0, 0);
                polyline.AddVertexAt(3, new Point2d(bbox.MinPoint.X - 1, origo.Y), 0, 0, 0);
                polyline.Closed = true;
                regions = Region.CreateFromCurves(new DBObjectCollection { polyline });
                if (regions.Count != 1) throw new Exception("Failed to create Region!");
                Region trimRegion = (Region)regions[0];
                mainRegion.BooleanOperation(BooleanOperationType.BoolSubtract, trimRegion);

                //Right side
                origo = new Point3d(origo.X + pv.StationEnd, origo.Y, 0.0);
                polyline = new Polyline(4);
                polyline.AddVertexAt(0, origo.To2d(), 0, 0, 0);
                polyline.AddVertexAt(1, new Point2d(origo.X, bbox.MaxPoint.Y + 1), 0, 0, 0);
                polyline.AddVertexAt(2, new Point2d(bbox.MaxPoint.X + 1, bbox.MaxPoint.Y + 1), 0, 0, 0);
                polyline.AddVertexAt(3, new Point2d(bbox.MaxPoint.X + 1, origo.Y), 0, 0, 0);
                polyline.Closed = true;
                regions = Region.CreateFromCurves(new DBObjectCollection { polyline });
                if (regions.Count != 1) throw new Exception("Failed to create Region!");
                trimRegion = (Region)regions[0];
                mainRegion.BooleanOperation(BooleanOperationType.BoolSubtract, trimRegion);
            }

            //test = mainRegion;

            //3.extract "lower" polyline
            if (mainRegion == null) throw new Exception("no merged polygon found!");

            using DBObjectCollection exploded = new DBObjectCollection();
            mainRegion.Explode(exploded);

            var ents = exploded.Cast<Entity>().ToList();
            List<Entity> filteredEntities = new();
            var pvBbox = pv.GeometricExtents;

            Point3d pvOrigo = new Point3d(pv.Location.X, pv.Location.Y, 0.0);
            Point3d pvEnd = new Point3d(pv.Location.X + pv.StationEnd, pv.Location.Y, 0.0);
            double tol = 0.01; //Tolerance for line ends

            foreach (var ent in ents)
            {
                switch (ent)
                {
                    case Line line:
                        {
                            //Do not take (vertical) lines at start and end
                            if ((line.StartPoint.X < pvOrigo.X + tol && line.EndPoint.X < pvOrigo.X + tol) ||
                                (line.StartPoint.X > pvEnd.X - tol && line.EndPoint.X > pvEnd.X - tol)) continue;

                            //Do not take lines that are outside the profile view
                            if (!pvBbox.IsPointIn(line.StartPoint) &&
                                !pvBbox.IsPointIn(line.EndPoint)) continue;

                            filteredEntities.Add(line);
                        }
                        break;
                    case Arc arc:
                        {
                            // Do not take(vertical) lines at start and end
                            if ((arc.StartPoint.X < pvOrigo.X + tol && arc.EndPoint.X < pvOrigo.X + tol) ||
                                (arc.StartPoint.X > pvEnd.X - tol && arc.EndPoint.X > pvEnd.X - tol)) continue;

                            //Do not take lines that are outside the profile view
                            if (!pvBbox.IsPointIn(arc.StartPoint) &&
                                !pvBbox.IsPointIn(arc.EndPoint)) continue;

                            filteredEntities.Add(arc);
                        }
                        break;
                    default:
                        break;
                }
            }

            var sorted = filteredEntities
                .OrderBy(x => x.GeometricExtents.MinPoint.X)
                .ToList();

            Polyline lowerpline = new Polyline();
            Point3d previousEnd = Point3d.Origin;
            for (int i = 0; i < sorted.Count; i++)
            {
                var ent = sorted[i];

                switch (ent)
                {
                    case Line line:
                        {
                            if (previousEnd.DistanceTo(line.StartPoint) > 0.00001 && i != 0)
                            {
                                //Check if the line is connected to the previous line
                                //If not, add a vertex at the end of the previous line
                                lowerpline.AddVertexAt(
                                    lowerpline.NumberOfVertices,
                                    previousEnd.To2d(), 0, 0, 0);
                            }

                            lowerpline.AddVertexAt(
                                lowerpline.NumberOfVertices,
                                line.StartPoint.To2d(), 0, 0, 0);

                            previousEnd = line.EndPoint;
                        }
                        break;
                    case Arc arc:
                        {
                            if (previousEnd.DistanceTo(arc.StartPoint) > 0.00001 && i != 0)
                            {
                                //Check if the arc is connected to the previous line
                                //If not, add a vertex at the end of the previous line
                                lowerpline.AddVertexAt(
                                    lowerpline.NumberOfVertices,
                                    previousEnd.To2d(), 0, 0, 0);
                            }

                            lowerpline.AddVertexAt(
                                lowerpline.NumberOfVertices,
                                arc.StartPoint.To2d(), arc.GetBulge(), 0, 0);

                            previousEnd = arc.EndPoint;
                        }
                        break;
                    default:
                        throw new Exception(
                            $"Unsupported entity type {ent.GetType()} found in the merged region!");
                }

                //Handle last point
                var lastPt = lowerpline.GetPoint2dAt(lowerpline.NumberOfVertices - 1);
                switch (ent)
                {
                    case Line line:
                        if (line.EndPoint.DistanceTo(lastPt.To3d()) > 0.00001)
                        {
                            lowerpline.AddVertexAt(
                                lowerpline.NumberOfVertices,
                                line.EndPoint.To2d(), 0, 0, 0);
                        }
                        previousEnd = line.EndPoint;
                        break;
                    case Arc arc:
                        if (arc.EndPoint.DistanceTo(lastPt.To3d()) > 0.00001)
                        {
                            lowerpline.AddVertexAt(
                                lowerpline.NumberOfVertices,
                                arc.EndPoint.To2d(), 0, 0, 0);
                        }
                        previousEnd = arc.EndPoint;
                        break;
                    default:
                        break;
                }
            }

            UnfilletedPolyline = lowerpline;
        }
        internal void FilletPolyline()
        {
            if (ProfileView == null) throw new Exception("No profile view found for the pipeline!");
            if (SizeArray == null) throw new Exception("No size array found for the pipeline!");

            if (UnfilletedPolyline == null)
            {
                FilletedPolyline = null;
                return;
            }

            double station = 0, elevation = 0;
            var filleter = AutoProfileFilleter.CreateDefault(sampleRadius);

            double sampleRadius(Point2d pt)
            {
                //Get the station and elevation at the point
                ProfileView!.ProfileView.FindStationAndElevationAtXY(pt.X, pt.Y, ref station, ref elevation);
                var size = SizeArray!.GetSizeAtStation(station);
                return size.VerticalMinRadius;
            }

            FilletedPolyline = filleter.PerformFilleting(UnfilletedPolyline);
        }
        public void Serialize(string filename)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            options.Converters.Add(new PolylineJsonConverter());
            options.Converters.Add(new Extents2dJsonConverter());
            var json = JsonSerializer.Serialize(this, options);
            System.IO.File.WriteAllText(filename, json);
        }
    }
}