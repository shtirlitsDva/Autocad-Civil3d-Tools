using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;

using Align = AutoProfileSolver.Alignment;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfileV2
{
    /// <summary>
    /// Solves the longitudinal pipe profile IN-PROCESS with the native C# alignment solver
    /// (the v4 port under Solver/), replacing the v2 Python subprocess. Maps
    /// <see cref="AP2_PipelineData"/> → <c>CaseSpec</c>, runs the solver, and renders the
    /// emitted Line/Arc chain as a profile-view <see cref="Polyline"/> (arc bulge = tan(sweep/4),
    /// exactly as the previous client did from sweep_rad).
    /// </summary>
    internal sealed class AutoProfileV2NativeSolver
    {
        private readonly Action<object> _log;

        public AutoProfileV2NativeSolver(Action<object> log) => _log = log;

        public Polyline SolveProfilePolyline(AP2_PipelineData data)
        {
            Align.CaseSpec spec = BuildCaseSpec(data);
            var settings = new Align.AlignmentSettings();           // cover_allowance default 0.02
            Align.AlignmentResult result = Align.Solver.SolveAlignment(spec, settings);
            Align.AlignmentSummary s = Align.Solver.Summarize(spec, result, settings.CoverAllowanceM);

            _log($"AutoProfile {data.Name}: success={s.Success} arcs={s.Arcs} lines={s.Lines} " +
                 $"C1={s.C1MaxDeg:0.###}deg cover={s.CoverViolationM:0.###}m rdef={s.RadiusDeficitM:0.###}m " +
                 $"intr={s.UtilityIntrusions} gap={s.CoverageGapM:0.###}m area={s.ExcavationAreaM2:0.#}m2 " +
                 $"overRouted={s.NOverRouted}");
            if (!s.Success)
                _log($"AutoProfile {data.Name}: NON-CLEAN result — drawing anyway (gated on segments).");

            return BuildPolyline(data, result.Chain);
        }

        private static Align.CaseSpec BuildCaseSpec(AP2_PipelineData data)
        {
            if (data.SurfaceProfile == null) throw new System.Exception($"No surface profile for {data.Name}.");
            if (data.SizeArray == null) throw new System.Exception($"No size array for {data.Name}.");

            var sp = data.SurfaceProfile.GetSimplifiedProfileDTO()
                .OrderBy(p => p[0]).ToArray();
            double[] sx = sp.Select(p => p[0]).ToArray();
            double[] sy = sp.Select(p => p[1]).ToArray();

            var sizes = data.SizeArray.Sizes
                .OrderBy(z => z.StartStation)
                .Select(z => new Align.PipeSize(z.StartStation, z.EndStation, z.VerticalMinRadius, z.Kod / 1000.0))
                .ToList();
            if (sizes.Count == 0) throw new System.Exception($"No pipe sizes for {data.Name}.");

            // AP2_Utility.Box is [MinX, MinY, MaxX, MaxY] == [s_lo, y_lo, s_hi, y_hi].
            var utils = (data.Utility ?? new List<AP2_Utility>())
                .Select(u => new Align.UtilityBox(
                    Math.Min(u.Box[0], u.Box[2]), Math.Max(u.Box[0], u.Box[2]),
                    Math.Min(u.Box[1], u.Box[3]), Math.Max(u.Box[1], u.Box[3])))
                .ToList();

            var forbidden = (data.HorizontalArcs?.HorizontalArcs ?? new List<AP2_HorizontalArc>())
                .Select(a => (Math.Min(a.StartStation, a.EndStation), Math.Max(a.StartStation, a.EndStation)))
                .ToList<(double, double)>();

            // Classify utilities (below_only vs either) from the cover target, as the loaders do.
            var provisional = new Align.CaseSpec(data.Name, sx, sy, sizes, utils, forbidden);
            var classified = utils.Select(b => Classify(b, provisional)).ToList();
            return new Align.CaseSpec(data.Name, sx, sy, sizes, classified, forbidden);
        }

        private static Align.UtilityBox Classify(Align.UtilityBox box, Align.CaseSpec spec)
        {
            if (box.TopologyKind == "below_only" || box.SHi <= box.SLo) return box;
            double tgtMin = double.PositiveInfinity;
            for (int i = 0; i < 16; i++)
            {
                double st = box.SLo + (box.SHi - box.SLo) * (i / 15.0);
                tgtMin = Math.Min(tgtMin, spec.TargetAt(st));
            }
            return box with { TopologyKind = box.YHi >= tgtMin ? "below_only" : "either" };
        }

        private Polyline BuildPolyline(AP2_PipelineData data, Align.Chain chain)
        {
            if (data.ProfileView == null) throw new System.Exception($"No profile view for {data.Name}.");
            if (chain.Primitives.Count == 0)
                throw new System.Exception($"Solver produced no segments for {data.Name}.");

            ProfileView pv = data.ProfileView.ProfileView;
            var polyline = new Polyline();

            foreach (var prim in chain.Primitives)
            {
                Align.Pt[] pts = prim.Points(2);                    // start, end
                Point2d start = ToProfileViewPoint(pv, pts[0]);
                Point2d end = ToProfileViewPoint(pv, pts[pts.Length - 1]);

                if (polyline.NumberOfVertices == 0)
                    polyline.AddVertexAt(0, start, 0.0, 0.0, 0.0);
                else if (!PointsEqual(polyline.GetPoint2dAt(polyline.NumberOfVertices - 1), start))
                    polyline.AddVertexAt(polyline.NumberOfVertices, start, 0.0, 0.0, 0.0);

                int bulgeIndex = polyline.NumberOfVertices - 1;
                double bulge = prim is Align.Arc arc ? Math.Tan(arc.SweepAngle / 4.0) : 0.0;
                polyline.SetBulgeAt(bulgeIndex, bulge);

                if (!PointsEqual(polyline.GetPoint2dAt(polyline.NumberOfVertices - 1), end))
                    polyline.AddVertexAt(polyline.NumberOfVertices, end, 0.0, 0.0, 0.0);
            }

            if (polyline.NumberOfVertices < 2)
                throw new System.Exception($"Pipe profile polyline for {data.Name} has fewer than two vertices.");

            return polyline;
        }

        private static Point2d ToProfileViewPoint(ProfileView pv, Align.Pt p)
        {
            double x = 0.0, y = 0.0;
            pv.FindXYAtStationAndElevation(p.X, p.Y, ref x, ref y);
            return new Point2d(x, y);
        }

        private static bool PointsEqual(Point2d a, Point2d b) => a.GetDistanceTo(b) < 1e-6;
    }
}
