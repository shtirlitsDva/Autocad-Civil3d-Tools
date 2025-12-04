using Autodesk.AutoCAD.Geometry;

using System.Collections.Generic;
using System.Linq;

using NTRExport.CadExtraction;

using static IntersectUtilities.UtilsCommon.Utils;

namespace NTRExport.Routing
{
    internal static class RoutedTopologyBuilder
    {
        public static void Build(RoutedGraph graph)
        {
            graph.Nodes.Clear();
            graph.EndpointMap.Clear();

            foreach (var member in graph.Members)
            {
                var endpointCount = CountEndpoints(member);
                if (endpointCount <= 0) continue;
                graph.EndpointMap[member] = new RoutedEndpoint[endpointCount];
            }

            foreach (var member in graph.Members)
            {
                foreach (var (point, index) in EnumerateEndpoints(member))
                {
                    var node = FindOrCreateNode(graph.Nodes, point);
                    var endpoint = new RoutedEndpoint(member, index, point)
                    {
                        Node = node
                    };
                    node.Endpoints.Add(endpoint);

                    if (graph.EndpointMap.TryGetValue(member, out var slots) &&
                        index >= 0 && index < slots.Length)
                    {
                        slots[index] = endpoint;
                    }
                }
            }
        }

        private static RoutedNode FindOrCreateNode(List<RoutedNode> nodes, Point3d point)
        {
            var tol = CadTolerance.Tol;
            var existing = nodes.FirstOrDefault(n => n.Position.DistanceTo(point) <= tol);
            if (existing != null)
                return existing;
            var node = new RoutedNode(point);
            nodes.Add(node);
            return node;
        }

        private static int CountEndpoints(RoutedMember member) => member switch
        {
            RoutedStraight => 2,
            RoutedBend => 2,
            RoutedReducer => 2,
            RoutedValve => 2,
            RoutedTee => 4,
            RoutedRigid => 2,
            _ => 0,
        };

        private static IEnumerable<(Point3d point, int index)> EnumerateEndpoints(RoutedMember member)
        {
            switch (member)
            {
                case RoutedStraight rs:
                    yield return (rs.A, 0);
                    yield return (rs.B, 1);
                    break;
                case RoutedBend rb:
                    yield return (rb.A, 0);
                    yield return (rb.B, 1);
                    break;
                case RoutedReducer red:
                    yield return (red.P1, 0);
                    yield return (red.P2, 1);
                    break;
                case RoutedValve valve:
                    yield return (valve.P1, 0);
                    yield return (valve.P2, 1);
                    break;
                case RoutedTee tee:
                    yield return (tee.Ph1, 0);
                    yield return (tee.Ph2, 1);
                    yield return (tee.Pa1, 2);
                    yield return (tee.Pa2, 3);
                    break;
                case RoutedRigid rigid:
                    yield return (rigid.P1, 0);
                    yield return (rigid.P2, 1);
                    break;
            }
        }
    }
}

