using Autodesk.Civil.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using Exception = System.Exception;
using Dreambuild.AutoCAD;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal class AP_SurfaceProfileData : PipelineDataBase
    {
        private static double DouglaPeukerTolerance = 0.1;
        [JsonInclude]
        public string Name { get; set; }
        [JsonIgnore]
        public Profile? Profile { get; set; }
        [JsonIgnore]
        public Polyline? SurfacePolylineFull { get; set; } = null;
        [JsonInclude]
        public Polyline? SurfacePolylineSimplified { get; set; } = null;
        [JsonIgnore]
        public Polyline? SurfacePolylineWithHangingEnds { get; set; } = null;
        private Polyline? offsetCentrelines;
        [JsonIgnore]
        public Polyline? OffsetCentrelines => offsetCentrelines;
        public AP_SurfaceProfileData(string name, Profile p, AP_PipelineData pipeline) : base(pipeline)
        {
            Name = name;

            Profile = p;

            SurfacePolylineFull = p.ToPolyline(pipeline.ProfileView!.ProfileView);

            SurfacePolylineSimplified = SurfacePolylineFull.GetDouglasPeukerReducedCopy(DouglaPeukerTolerance);
            if (SurfacePolylineSimplified == null) throw new Exception($"No reduced polyline found for {Name}!");

            //Add hanging start and end segments to catch arcs that are too close
            //to the start and end of the profile view

            Polyline pline = SurfacePolylineSimplified.Clone() as Polyline;

            var start = pline.GetPoint2dAt(0);
            var addStart = new Point2d(start.X, start.Y - 50);
            pline.AddVertexAt(0, addStart, 0, 0, 0);

            var end = pline.GetPoint2dAt(pline.NumberOfVertices - 1);
            var addEnd = new Point2d(end.X, end.Y - 50);
            pline.AddVertexAt(pline.NumberOfVertices, addEnd, 0, 0, 0);

            SurfacePolylineWithHangingEnds = pline;

            BuildOffsetCentrelines(pipeline.SizeArray!);
        }

        private void BuildOffsetCentrelines(IPipelineSizeArrayV2 sizeArray)
        {
            if (SurfacePolylineFull == null) throw new Exception($"No surface polyline found for {Name}!");

            DoubleCollection splitDoubles = new();

            if (sizeArray.Length > 1)
            {
                for (int i = 0; i < sizeArray.Length - 1; i++)
                {//-1 to avoid adding a split point at the end of polyline
                    splitDoubles.Add(
                        SurfacePolylineSimplified.GetParameterAtStationX(
                            sizeArray[i].EndStation));
                }
            }

            List<(SizeEntryV2, Polyline)> polylinesToOffset = new();

            if (splitDoubles.Count == 0)
            {
                polylinesToOffset.Add((sizeArray[0], SurfacePolylineSimplified));
            }
            else
            {
                using var splitCurves = SurfacePolylineSimplified.GetSplitCurves(splitDoubles);
                //Check for sanity
                if (!(splitCurves.Count == sizeArray.Length))
                    throw new Exception($"The number of split curves {splitCurves.Count} does not match" +
                        $" the number of sizes {sizeArray.Length}!");

                for (int i = 0; i < splitCurves.Count; i++)
                {
                    var current = splitCurves[i] as Polyline;
                    if (current == null) throw new Exception($"The split curve {i} is not a polyline!");

                    var size = sizeArray[i];

                    polylinesToOffset.Add((size, current));
                }
            }

            List<Polyline> offsets = new();
            for (int i = 0; i < polylinesToOffset.Count; i++)
            {
                if (polylinesToOffset[i].Item2 == null)
                    throw new Exception($"The polyline {i} is null for {Name}!");

                var size = polylinesToOffset[i].Item1;
                var pline = polylinesToOffset[i].Item2;

                double offset =
                    PipeScheduleV2.PipeScheduleV2.GetCoverDepth(size.DN, size.System, size.Type) +
                    size.Kod / 1000 / 2;

                Polyline offsetPline = (Polyline)pline.Clone();

                offsetPline.TransformBy(Matrix3d.Displacement(
                    new Vector3d(0, -offset, 0)));
                offsets.Add(offsetPline);
            }

            if (offsets.Count == 0) throw new Exception($"No offset polylines found for {Name}!");
            if (offsets.Count == 1) offsetCentrelines = offsets[0];
            else
            {
                Polyline combined = new Polyline();
                for (int i = 0; i < offsets.Count; i++)
                {
                    var cur = offsets[i];
                    for (int j = 0; j < cur.NumberOfVertices; j++)
                        combined.AddVertexAt(combined.NumberOfVertices, cur.GetPoint2dAt(j), 0, 0, 0);
                }
                offsetCentrelines = combined;
            }
        }
        /// <summary>
        /// x is station along the profile view. Returns the elevation at this station.
        /// </summary>
        internal double GetSurfaceYAtX(double x)
        {
            if (Profile == null) throw new Exception($"No profile found for {Name}!");
            return Profile.ElevationAt(x);
        }
        internal double RotationOfSurfaceSimplifiedAtPointProjected(Point3d point)
        {
            if (SurfacePolylineSimplified == null) throw new Exception("No surface polyline found for the pipeline!");

            var pline = SurfacePolylineSimplified;
            var pt = pline.GetClosestPointTo(point, Vector3d.YAxis, false);
            var deriv = pline.GetFirstDerivative(pt);

            var rotation = Math.Atan2(deriv.Y, deriv.X);

            return rotation;
        }
    }
}