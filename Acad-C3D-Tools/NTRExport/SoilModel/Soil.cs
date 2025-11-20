using Autodesk.AutoCAD.Geometry;

using NTRExport.CadExtraction;
using NTRExport.Enums;
using NTRExport.Routing;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static IntersectUtilities.UtilsCommon.Utils;

namespace NTRExport.SoilModel
{
    internal sealed class SoilProfile
    {
        public static SoilProfile Default { get; } = new SoilProfile(
            name: "Soil_Default",
            coverHeight: 0.6,
            groundWaterDistance: null,
            soilWeightAbove: null,
            soilWeightBelow: null,
            frictionAngleDeg: null,
            cushionType: null,
            cushionThickness: 0.0);

        public string Name { get; }
        public double CoverHeight { get; }
        public double? GroundWaterDistance { get; }
        public double? SoilWeightAbove { get; }
        public double? SoilWeightBelow { get; }
        public double? FrictionAngleDeg { get; }
        public int? CushionType { get; }
        public double CushionThk { get; }

        public SoilProfile(
            string name,
            double coverHeight,
            double? groundWaterDistance,
            double? soilWeightAbove,
            double? soilWeightBelow,
            double? frictionAngleDeg,
            int? cushionType,
            double cushionThickness)
        {
            Name = name;
            CoverHeight = coverHeight;
            GroundWaterDistance = groundWaterDistance;
            SoilWeightAbove = soilWeightAbove;
            SoilWeightBelow = soilWeightBelow;
            FrictionAngleDeg = frictionAngleDeg;
            CushionType = cushionType;
            CushionThk = cushionThickness;
        }
    }

    internal interface INtrSoilAdapter
    {
        IEnumerable<string> Define(SoilProfile p);    // header definitions
        string? RefToken(SoilProfile p);              // e.g., "UMG=Soil_C80"
    }

    internal sealed class SoilPlanner
    {
        private readonly RoutedGraph _graph;
        private readonly SoilProfile _defaultProfile;
        private readonly IReadOnlyDictionary<SoilHintKind, SoilProfile> _profiles;
        private readonly Dictionary<RoutedStraight, List<Span>> _spans = new();
        private const double Tol = 1e-6;

        public SoilPlanner(
            RoutedGraph graph,
            SoilProfile defaultProfile,
            IReadOnlyDictionary<SoilHintKind, SoilProfile> profiles)
        {
            _graph = graph;
            _defaultProfile = defaultProfile;
            _profiles = profiles;
        }

        public void Apply()
        {
            foreach (var straight in _graph.Members.OfType<RoutedStraight>())
                straight.Soil = _defaultProfile;

            if (_graph.SoilHints.Count == 0)
                return;

            foreach (var hint in _graph.SoilHints)
                ApplyHint(hint);

            SplitAndAssign();
            _graph.SoilHints.Clear();
        }

        private void ApplyHint(SoilHint hint)
        {
            if (!_profiles.ContainsKey(hint.Kind)) return;
            var node = FindNode(hint.AnchorPoint);
            if (node == null) return;

            var seen = new Dictionary<(RoutedMember member, int endpoint), double>();

            foreach (var endpoint in node.Endpoints)
            {
                if (!ShouldFollow(endpoint, hint)) continue;
                TraverseMember(endpoint.Member, endpoint.Index, hint, hint.ReachMeters, seen);
            }
        }

        private void TraverseMember(
            RoutedMember member,
            int fromIndex,
            SoilHint hint,
            double remaining,
            Dictionary<(RoutedMember member, int endpoint), double> seen)
        {
            if (remaining <= Tol) return;

            var key = (member, fromIndex);
            if (seen.TryGetValue(key, out var prev) && prev >= remaining - Tol)
                return;
            seen[key] = remaining;

            double leftover = remaining;
            if (member is RoutedStraight straight)
                leftover = ApplyStraightSpan(straight, fromIndex, remaining, hint.Kind);

            if (leftover <= Tol)
                return;

            foreach (var exitNode in ExitNodes(member, fromIndex))
            {
                if (exitNode == null) continue;
                foreach (var endpoint in exitNode.Endpoints)
                {
                    if (ReferenceEquals(endpoint.Member, member)) continue;
                    if (!ShouldFollow(endpoint, hint)) continue;
                    TraverseMember(endpoint.Member, endpoint.Index, hint, leftover, seen);
                }
            }
        }

        private double ApplyStraightSpan(RoutedStraight straight, int fromIndex, double remaining, SoilHintKind kind)
        {
            var length = straight.Length;
            if (length <= Tol) return remaining;

            var cover = Math.Min(remaining, length);
            var start = fromIndex == 0 ? 0.0 : length - cover;
            var end = fromIndex == 0 ? cover : length;
            AddSpan(straight, start, end, kind);
            return remaining - cover;
        }

        private IEnumerable<RoutedNode?> ExitNodes(RoutedMember member, int fromIndex)
        {
            if (!_graph.EndpointMap.TryGetValue(member, out var endpoints))
                yield break;

            foreach (var endpoint in endpoints)
            {
                if (endpoint == null || endpoint.Index == fromIndex)
                    continue;
                yield return endpoint.Node;
            }
        }

        private RoutedNode? FindNode(Point3d point)
        {
            var tol = CadTolerance.Tol;
            return _graph.Nodes.FirstOrDefault(n => n.Position.DistanceTo(point) <= tol);
        }

        private static bool ShouldFollow(RoutedEndpoint endpoint, SoilHint hint)
        {
            if (!hint.IncludeAnchorMember &&
                endpoint.Member.Emitter != null &&
                endpoint.Member.Emitter.Source == hint.SourceHandle)
                return false;

            if (hint.FlowRole != FlowRole.Unknown &&
                endpoint.Member.FlowRole != hint.FlowRole)
                return false;

            return true;
        }

        private void AddSpan(RoutedStraight straight, double start, double end, SoilHintKind kind)
        {
            var s = Math.Max(0.0, Math.Min(start, end));
            var e = Math.Min(straight.Length, Math.Max(start, end));
            if (e - s <= Tol) return;

            if (!_spans.TryGetValue(straight, out var list))
            {
                list = new();
                _spans[straight] = list;
            }
            list.Add(new Span(s, e, kind));
        }

        private void SplitAndAssign()
        {
            foreach (var kvp in _spans)
            {
                var straight = kvp.Key;
                var length = straight.Length;
                if (length <= Tol) continue;

                var cuts = new SortedSet<double> { 0.0, length };
                foreach (var span in kvp.Value)
                {
                    cuts.Add(Math.Max(0.0, Math.Min(length, span.Start)));
                    cuts.Add(Math.Max(0.0, Math.Min(length, span.End)));
                }

                var ordered = cuts.ToList();
                var newSegments = new List<RoutedStraight>();
                for (int i = 0; i < ordered.Count - 1; i++)
                {
                    var s0 = ordered[i];
                    var s1 = ordered[i + 1];
                    if (s1 - s0 <= Tol) continue;

                    var profile = SelectProfile(kvp.Value, 0.5 * (s0 + s1));
                    var segment = SliceStraight(straight, s0 / length, s1 / length, profile);
                    newSegments.Add(segment);
                }

                ReplaceStraight(straight, newSegments);
            }

            RoutedTopologyBuilder.Build(_graph);
        }

        private SoilProfile SelectProfile(List<Span> spans, double position)
        {
            var profile = _defaultProfile;
            var best = _defaultProfile.CushionThk;

            foreach (var span in spans)
            {
                if (position < span.Start - Tol || position > span.End + Tol)
                    continue;
                if (!_profiles.TryGetValue(span.Kind, out var candidate))
                    continue;
                if (candidate.CushionThk >= best)
                {
                    best = candidate.CushionThk;
                    profile = candidate;
                }
            }

            return profile;
        }

        private RoutedStraight SliceStraight(RoutedStraight original, double t0, double t1, SoilProfile profile)
        {
            var a = Interpolate(original.A, original.B, t0);
            var b = Interpolate(original.A, original.B, t1);
            return new RoutedStraight(original.Source, original.Emitter)
            {
                A = a,
                B = b,
                DN = original.DN,
                Material = original.Material,
                DnSuffix = original.DnSuffix,
                FlowRole = original.FlowRole,
                LTG = original.LTG,
                ZOffsetMeters = original.ZOffsetMeters,
                Soil = profile,
            };
        }

        private void ReplaceStraight(RoutedStraight original, List<RoutedStraight> replacement)
        {
            var index = _graph.Members.IndexOf(original);
            if (index < 0) return;
            _graph.Members.RemoveAt(index);
            _graph.Members.InsertRange(index, replacement);
        }

        private static Point3d Interpolate(Point3d a, Point3d b, double t)
        {
            var clamped = Math.Max(0.0, Math.Min(1.0, t));
            return new Point3d(
                a.X + clamped * (b.X - a.X),
                a.Y + clamped * (b.Y - a.Y),
                a.Z + clamped * (b.Z - a.Z));
        }

        private readonly record struct Span(double Start, double End, SoilHintKind Kind);
    }
}
