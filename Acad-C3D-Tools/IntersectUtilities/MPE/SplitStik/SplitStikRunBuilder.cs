using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.SplitStik
{
    /// <summary>
    /// One polyline to draw: a service line as-is, or a chain of main-line edges that share
    /// the same dimension merged into a single run.
    /// </summary>
    internal sealed record PipeRun(
        D2rDim Dim,
        bool IsServiceLine,
        Point2d[] Vertices,
        int EdgeCount,
        double SourceLength);

    internal sealed class SplitStikStats
    {
        public int TotalEdges;
        public int SkippedNoDim;
        public int SkippedDegenerateGeometry;
        public int SkippedNonFiniteGeometry;
        public int SkippedUnknownType;
        public int RunsWithoutWidth;
        public int SeamMismatches;
        public int MainEdgesUsed;
        public int ServiceEdgesUsed;
        public int SkippedUnknownFamily;
        public readonly HashSet<string> UnknownTypeTags = new(StringComparer.Ordinal);
        public readonly HashSet<string> UnknownFamilies = new(StringComparer.Ordinal);

        /// <summary>System/DN combinations PipeScheduleV2 has no width for in ANY series.</summary>
        public readonly HashSet<string> MissingWidths = new(StringComparer.Ordinal);

        /// <summary>Combinations whose width came from a series other than the preferred one.</summary>
        public readonly HashSet<string> SeriesFallbacks = new(StringComparer.Ordinal);
        public readonly SortedDictionary<int, int> EdgesPerSubGraph = new();

        public bool HasAnomalies =>
            SkippedNoDim > 0
            || SkippedDegenerateGeometry > 0
            || SkippedNonFiniteGeometry > 0
            || SkippedUnknownType > 0
            || SkippedUnknownFamily > 0
            || RunsWithoutWidth > 0
            || SeamMismatches > 0;
    }

    internal static class SplitStikRunBuilder
    {
        /// <summary>
        /// A vertex of an edge's geometry is expected to coincide exactly with the
        /// NodeJunction it meets — both come from the same source. This is only a guard
        /// against accumulated float drift, in metres.
        /// </summary>
        private const double NodeTolerance = 1e-4;

        internal static IReadOnlyList<PipeRun> Build(D2rGraph graph, SplitStikStats stats)
        {
            List<PipeRun> runs = new();
            IReadOnlyList<D2rEdge> edges = graph.Edges;
            List<int> mainEdges = new();

            for (int i = 0; i < edges.Count; i++)
            {
                D2rFeature feature = edges[i].Feature;
                stats.TotalEdges++;

                if (feature.SubGraphId != 0)
                {
                    stats.EdgesPerSubGraph.TryGetValue(feature.SubGraphId, out int count);
                    stats.EdgesPerSubGraph[feature.SubGraphId] = count + 1;
                }

                if (!feature.IsKnownType)
                {
                    stats.SkippedUnknownType++;
                    stats.UnknownTypeTags.Add(feature.Type);
                    continue;
                }

                // An absent Dim is how the .d2r records a segment that was never sized —
                // it supplies nothing. DimensioneringV2's own drafter skips these too.
                if (feature.Dim is null || feature.Dim.NominalDiameter <= 0)
                {
                    stats.SkippedNoDim++;
                    continue;
                }

                if (feature.Geometry.Length < 2)
                {
                    stats.SkippedDegenerateGeometry++;
                    continue;
                }

                // The .d2r may legally carry NaN/Infinity (AllowNamedFloatingPointLiterals, see
                // D2rReader), and every distance guard downstream compares FALSE on those rather
                // than catching them — so a non-finite vertex would reach Polyline.AddVertexAt and
                // either throw or write a corrupt polyline. Reject it here and say so in the report.
                if (!IsFinite(feature.Geometry))
                {
                    stats.SkippedNonFiniteGeometry++;
                    continue;
                }

                if (feature.IsServiceLine)
                {
                    // Service lines are never merged — one polyline per connection.
                    stats.ServiceEdgesUsed++;
                    runs.Add(
                        new PipeRun(feature.Dim, true, feature.Geometry, 1, feature.Length));
                    continue;
                }

                mainEdges.Add(i);
            }

            runs.AddRange(BuildMainRuns(graph, mainEdges, stats));
            return runs;
        }

        private static IEnumerable<PipeRun> BuildMainRuns(
            D2rGraph graph,
            List<int> mainEdges,
            SplitStikStats stats)
        {
            IReadOnlyList<D2rEdge> edges = graph.Edges;
            IReadOnlyList<D2rNode> vertices = graph.Vertices;

            // Adjacency over MAIN-LINE edges only. This is the whole point: a node where a
            // stikledning branches off has a stored Degree of 3, but its main-line degree is
            // 2, so a hovedledning run continues straight through the stik tee. The split
            // rule is "dimension changes", not "stik branch points".
            Dictionary<int, List<int>> adjacency = new();
            foreach (int i in mainEdges)
            {
                D2rEdge edge = edges[i];
                if (edge.SourceIndex == edge.TargetIndex) continue; // self-loop: never merged
                Add(adjacency, edge.SourceIndex, i);
                Add(adjacency, edge.TargetIndex, i);
            }

            HashSet<int> visited = new();

            foreach (int seed in mainEdges)
            {
                if (!visited.Add(seed)) continue;

                D2rEdge seedEdge = edges[seed];
                LinkedList<int> chain = new();
                chain.AddFirst(seed);

                Extend(seedEdge.TargetIndex, seed, append: true);
                Extend(seedEdge.SourceIndex, seed, append: false);

                List<int> ordered = chain.ToList();
                Point2d[] geometry = Stitch(ordered, edges, vertices);
                if (geometry.Length < 2)
                {
                    stats.SkippedDegenerateGeometry += ordered.Count;
                    continue;
                }

                stats.MainEdgesUsed += ordered.Count;
                yield return new PipeRun(
                    seedEdge.Feature.Dim!,
                    false,
                    geometry,
                    ordered.Count,
                    ordered.Sum(i => edges[i].Feature.Length));

                void Extend(int node, int fromEdge, bool append)
                {
                    int currentNode = node;
                    int currentEdge = fromEdge;

                    while (true)
                    {
                        if (!adjacency.TryGetValue(currentNode, out List<int>? incident)
                            || incident.Count != 2)
                            break; // branch (>=3) or dead end (1) — always split here

                        if (incident[0] != currentEdge && incident[1] != currentEdge) break;

                        int next = incident[0] == currentEdge ? incident[1] : incident[0];
                        if (next == currentEdge || visited.Contains(next)) break;

                        if (!SameDimension(edges[next].Feature.Dim, seedEdge.Feature.Dim)) break;

                        // Refuse to merge geometry that does not actually meet at this node,
                        // so Stitch below can never produce a bent run.
                        if (!Touches(edges[next].Feature.Geometry, vertices[currentNode].Location))
                        {
                            stats.SeamMismatches++;
                            break;
                        }

                        visited.Add(next);
                        if (append) chain.AddLast(next);
                        else chain.AddFirst(next);

                        currentNode = OtherEnd(edges[next], currentNode);
                        currentEdge = next;
                    }
                }
            }
        }

        /// <summary>
        /// Family must be part of the key, not just the diameter: Stål DN 65 and AluPEXFL 63
        /// are different pipes at a similar nominal size.
        /// </summary>
        private static bool SameDimension(D2rDim? a, D2rDim? b) =>
            a is not null
            && b is not null
            && a.NominalDiameter == b.NominalDiameter
            && string.Equals(a.FamilyName, b.FamilyName, StringComparison.Ordinal);

        private static void Add(Dictionary<int, List<int>> map, int key, int value)
        {
            if (!map.TryGetValue(key, out List<int>? list))
            {
                list = new List<int>(2);
                map[key] = list;
            }
            list.Add(value);
        }

        private static int OtherEnd(D2rEdge edge, int node) =>
            edge.SourceIndex == node ? edge.TargetIndex : edge.SourceIndex;

        private static bool IsFinite(Point2d[] geometry)
        {
            foreach (Point2d point in geometry)
                if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
                    return false;

            return true;
        }

        private static bool Touches(Point2d[] geometry, Point2d node) =>
            geometry.Length >= 2
            && (geometry[0].GetDistanceTo(node) <= NodeTolerance
                || geometry[^1].GetDistanceTo(node) <= NodeTolerance);

        /// <summary>
        /// Concatenates the chain's per-edge vertex lists into one. An edge's vertices are not
        /// guaranteed to run Source-to-Target, so each is oriented against the node it is
        /// entered from, and the duplicated seam vertex is dropped.
        /// </summary>
        private static Point2d[] Stitch(
            IReadOnlyList<int> chain,
            IReadOnlyList<D2rEdge> edges,
            IReadOnlyList<D2rNode> vertices)
        {
            int currentNode;
            if (chain.Count == 1)
            {
                currentNode = edges[chain[0]].SourceIndex;
            }
            else
            {
                D2rEdge first = edges[chain[0]];
                D2rEdge second = edges[chain[1]];
                int shared =
                    first.SourceIndex == second.SourceIndex
                    || first.SourceIndex == second.TargetIndex
                        ? first.SourceIndex
                        : first.TargetIndex;
                currentNode = OtherEnd(first, shared);
            }

            List<Point2d> result = new();

            foreach (int edgeIndex in chain)
            {
                D2rEdge edge = edges[edgeIndex];
                Point2d[] geometry = edge.Feature.Geometry;
                Point2d entry = vertices[currentNode].Location;

                bool reverse =
                    geometry[^1].GetDistanceTo(entry) < geometry[0].GetDistanceTo(entry);

                for (int i = 0; i < geometry.Length; i++)
                {
                    Point2d point = reverse ? geometry[geometry.Length - 1 - i] : geometry[i];
                    if (result.Count > 0
                        && result[^1].GetDistanceTo(point) <= NodeTolerance)
                        continue; // seam vertex, or a coincident duplicate
                    result.Add(point);
                }

                currentNode = OtherEnd(edge, currentNode);
            }

            return result.ToArray();
        }
    }
}
