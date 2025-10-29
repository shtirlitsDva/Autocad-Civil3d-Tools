using Autodesk.AutoCAD.Geometry;

using NTRExport.Ntr;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRExport.SoilModel
{
    internal class SoilProfile
    {
        public static readonly SoilProfile Default = new("Soil_Default", 0.00);
        public string Name { get; }
        public double CushionThk { get; }            // m
                                                     // extend with k, phi, gamma, bedding class…
        public SoilProfile(string name, double cushionThk) { Name = name; CushionThk = cushionThk; }
    }

    internal class SoilRule
    {
        public Type AppliesTo;                        // typeof(NtrBend), typeof(NtrTee), etc.
        public double Reach;                          // m along pipeline away from the fitting
        public SoilProfile Profile;
    }

    internal interface INtrSoilAdapter
    {
        IEnumerable<string> Define(SoilProfile p);    // header definitions
        string? RefToken(SoilProfile p);              // e.g., "UMG=Soil_C80"
    }

    internal sealed class SoilPlanner
    {
        private readonly NtrGraph _g;
        private readonly SoilProfile _defaultSoil;
        private readonly IReadOnlyList<SoilRule> _rules;
        private const double Tol = 0.005;

        public SoilPlanner(NtrGraph g, SoilProfile @default, IEnumerable<SoilRule> rules)
        { _g = g; _defaultSoil = @default; _rules = rules.ToList(); }

        public void Apply()
        {
            // Assign default to all pipes
            foreach (var p in _g.Members.OfType<NtrPipe>())
                p.Soil = _defaultSoil;

            // Collect split marks per pipe
            var marks = new Dictionary<NtrPipe, SortedSet<double>>();

            foreach (var m in _g.Members)
            {
                var rule = _rules.FirstOrDefault(r => r.AppliesTo.IsInstanceOfType(m));
                if (rule == null) continue;

                foreach (var (pipe, sFrom) in IncidentPipes(m))
                {
                    var (ok, sSplit) = FindSplitOnPipesForward(pipe, sFrom, rule.Reach);
                    if (!ok) continue; // reach exhausted within fittings only

                    if (!marks.TryGetValue(pipe, out var set)) marks[pipe] = set = new() { 0.0, pipe.Length };
                    set.Add(Math.Clamp(sSplit, 0, pipe.Length));

                    // Assign soil on [sFrom, sSplit]; overlapping picks thicker cushion
                    // We defer assignment to SplitAndAssign to keep single responsibility
                }
            }

            // Replace affected pipes with split children and assign soils segment-wise
            foreach (var kv in marks)
                SplitAndAssign(kv.Key, kv.Value, SegmentSoilResolver(kv.Key));
        }

        // Find distance sSplit along the chain starting at sFrom on 'pipe', skipping fittings.
        private (bool ok, double s) FindSplitOnPipesForward(NtrPipe pipe, double sFrom, double reach)
        {
            // Simple case: reach fits within this pipe
            var remaining = pipe.Length - sFrom;
            if (reach <= remaining + Tol) return (true, sFrom + reach);

            // Otherwise move across fitting and continue on the most colinear next pipe
            var next = NextPipeAfter(pipe);
            if (next is null) return (true, pipe.Length); // clamp at end
            var leftover = Math.Max(0.0, reach - remaining);
            return FindSplitOnPipesForward(next.Value.pipe, 0.0, leftover);
        }

        private (NtrPipe pipe, double angle)? NextPipeAfter(NtrPipe p)
        {
            // Topology-free stub: you likely have node/port graph to query.
            // Implement: find fittings touching p.B, pick outgoing pipe with max dot product to (p.B - p.A).
            return null;
        }

        private IEnumerable<(NtrPipe pipe, double sFrom)> IncidentPipes(NtrMember m)
        {
            // Without explicit nodes, incident means pipes whose A or B equals a member end.
            if (m is NtrBend b)
            {
                foreach (var p in _g.Members.OfType<NtrPipe>())
                {
                    if (Equal(p.A, b.A) || Equal(p.B, b.A)) yield return (p, Equal(p.A, b.A) ? 0.0 : p.Length);
                    if (Equal(p.A, b.B) || Equal(p.B, b.B)) yield return (p, Equal(p.A, b.B) ? 0.0 : p.Length);
                }
            }
            if (m is NtrTee t)
            {
                foreach (var end in new[] { t.Ph1, t.Ph2, t.Pa1, t.Pa2 })
                    foreach (var p in _g.Members.OfType<NtrPipe>())
                        if (Equal(p.A, end) || Equal(p.B, end)) yield return (p, Equal(p.A, end) ? 0.0 : p.Length);
            }
        }

        private static bool Equal(Point3d a, Point3d b) => Math.Abs(a.X - b.X) <= Tol && Math.Abs(a.Y - b.Y) <= Tol;

        // Decide soil per segment [s0,s1] from overlapping rules; thicker cushion wins.
        private Func<double, double, SoilProfile> SegmentSoilResolver(NtrPipe basis) => (s0, s1) =>
        {
            // Hook: compute overlaps with rules anchored at nearby fittings if needed.
            // Minimal version: if a split exists it was caused by some rule → use non-default.
            return basis.Soil; // placeholder; you can keep a zone map if you wish
        };

        private void SplitAndAssign(NtrPipe pipe, SortedSet<double> cuts, Func<double, double, SoilProfile> soilOf)
        {
            var list = cuts.ToList();
            var idx = _g.Members.IndexOf(pipe);
            _g.Members.RemoveAt(idx);

            for (int i = 0; i < list.Count - 1; i++)
            {
                var a = list[i]; var b = list[i + 1];
                var (pa, pb) = Interpolate(pipe, a);
                var (pa2, pb2) = Interpolate(pipe, b);
                var seg = pipe.With(pa, pb2, soilOf(a, b));
                _g.Members.Insert(idx++, seg);
            }
        }

        private static (Point3d, Point3d) Interpolate(NtrPipe p, double s)
        {
            var t = (p.Length <= 1e-9) ? 0.0 : s / p.Length;
            var x = p.A.X + t * (p.B.X - p.A.X);
            var y = p.A.Y + t * (p.B.Y - p.A.Y);
            return (new Point3d(x, y, 0), new Point3d(x, y, 0));
        }
    }
}
