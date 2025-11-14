using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Routing;
using NTRExport.SoilModel;

using static IntersectUtilities.UtilsCommon.Utils;
using static NTRExport.Utils.Utils;


namespace NTRExport.TopologyModel
{
    internal class TNode
    {
        public Point3d Pos { get; init; }
        public string Name { get; set; } = ""; // assigned later
        public List<TPort> Ports { get; } = new();
    }

    internal class TPort
    {
        public PortRole Role { get; init; }
        public TNode Node { get; init; }
        public ElementBase Owner { get; init; }

        public TPort(PortRole role, TNode node, ElementBase owner)
        {
            Role = role;
            Node = node;
            Owner = owner;
            node.Ports.Add(this);
        }
    }

    internal class Topology
    {
        public List<TNode> Nodes { get; } = new();
        public List<ElementBase> Elements { get; } = new();

        public IEnumerable<TPipe> Pipes => Elements.OfType<TPipe>();
        public IEnumerable<TFitting> Fittings => Elements.OfType<TFitting>();

        public TPipe? FindPipeAtNodes(TNode nodeA, TNode? nodeB = null)
        {
            foreach (var pipe in Pipes)
            {
                if (pipe.A.Node == nodeA || pipe.B.Node == nodeA)
                {
                    if (nodeB == null || pipe.A.Node == nodeB || pipe.B.Node == nodeB)
                    {
                        return pipe;
                    }
                }
            }

            return null;
        }

        public int InferMainDn(TFitting fitting)
        {
            var dns = new List<int>();
            foreach (var node in fitting.Ports.Select(p => p.Node))
            {
                foreach (var pipe in Pipes)
                {
                    if (pipe.A.Node == node || pipe.B.Node == node)
                    {
                        dns.Add(pipe.DN);
                    }
                }
            }
            return dns.Count > 0 ? dns.Max() : 200;
        }        

        public int InferDn1(TFitting fitting) => InferMainDn(fitting);

        public int InferDn2(TFitting fitting)
        {
            var dns = new List<int>();
            foreach (var node in fitting.Ports.Select(p => p.Node))
            {
                foreach (var pipe in Pipes)
                {
                    if (pipe.A.Node == node || pipe.B.Node == node)
                    {
                        dns.Add(pipe.DN);
                    }
                }
            }
            return dns.Count > 1 ? dns.Min() : 100;
        }

        private static bool TryReadReducerDeclaredDns(Reducer red, out int dn1, out int dn2)
        {
            dn1 = 0; dn2 = 0;
            var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument.Database;

            var br = red.Source.Go<BlockReference>(db);
            if (br == null) return false;

            var s1 = br.ReadDynamicCsvProperty(DynamicProperty.DN1);
            var s2 = br.ReadDynamicCsvProperty(DynamicProperty.DN2);
            if (!int.TryParse(s1, out var d1)) return false;
            if (!int.TryParse(s2, out var d2)) return false;
            if (d2 > d1) { var t = d1; d1 = d2; d2 = t; }
            dn1 = d1; dn2 = d2;
            return true;
        }

        private int? ResolveNeighborDnForPort(ElementBase owner, TPort port)
        {
            foreach (var el in Elements)
            {
                if (ReferenceEquals(el, owner)) continue;

                foreach (var np in el.Ports)
                {
                    if (!ReferenceEquals(np.Node, port.Node)) continue;

                    if (el is TPipe tp) return tp.DN;

                    if (el is TFitting tf)
                    {
                        if (tf.TryGetDnForPortRole(np.Role, out var d)) return d;
                        return tf.DN;
                    }
                }
            }
            return null;
        }

        public bool TryOrientReducer(Reducer red, out int dn1, out int dn2, out Point3d P1, out Point3d P2)
        {
            dn1 = 0; dn2 = 0; P1 = default; P2 = default;

            var pr = red.Ports.Take(2).ToArray();
            if (pr.Length < 2) return false;
            var pa = pr[0];
            var pb = pr[1];

            var declaredOk = TryReadReducerDeclaredDns(red, out var decl1, out var decl2);

            var dnA = ResolveNeighborDnForPort(red, pa);
            var dnB = ResolveNeighborDnForPort(red, pb);

            if (declaredOk)
            {
                if (dnA.HasValue)
                {
                    if (dnA.Value == decl1)
                    {
                        dn1 = decl1; dn2 = decl2; P1 = pa.Node.Pos; P2 = pb.Node.Pos; return true;
                    }
                    if (dnA.Value == decl2)
                    {
                        dn1 = decl1; dn2 = decl2; P1 = pb.Node.Pos; P2 = pa.Node.Pos; return true;
                    }
                }

                if (dnB.HasValue)
                {
                    if (dnB.Value == decl1)
                    {
                        dn1 = decl1; dn2 = decl2; P1 = pb.Node.Pos; P2 = pa.Node.Pos; return true;
                    }
                    if (dnB.Value == decl2)
                    {
                        dn1 = decl1; dn2 = decl2; P1 = pa.Node.Pos; P2 = pb.Node.Pos; return true;
                    }
                }
            }

            if (dnA.HasValue && dnB.HasValue)
            {
                if (dnA.Value == dnB.Value)
                {
                    throw new InvalidOperationException($"Reducer {red.Source}: neighbors report same DN {dnA.Value} on both sides.");
                }

                if (dnA.Value >= dnB.Value)
                {
                    dn1 = dnA.Value; dn2 = dnB.Value; P1 = pa.Node.Pos; P2 = pb.Node.Pos;
                }
                else
                {
                    dn1 = dnB.Value; dn2 = dnA.Value; P1 = pb.Node.Pos; P2 = pa.Node.Pos;
                }
                return true;
            }

            return false;
        }

        internal FlowRole FindRoleFromPort(TFitting requester, TPort startPort)
        {
            if (startPort?.Node == null) return FlowRole.Unknown;

            FlowRole FlowFromType(PipeTypeEnum pipeType) =>
                pipeType switch
                {
                    PipeTypeEnum.Frem => FlowRole.Supply,
                    PipeTypeEnum.Retur => FlowRole.Return,
                    _ => FlowRole.Unknown,
                };

            var visited = new HashSet<TNode>();
            var queue = new Queue<TNode>();
            visited.Add(startPort.Node);
            queue.Enqueue(startPort.Node);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                foreach (var port in node.Ports)
                {
                    var owner = port.Owner;
                    if (ReferenceEquals(owner, requester)) continue;

                    if (owner is TFitting fitting)
                    {
                        if (ReferenceEquals(fitting, requester)) continue;
                        if (fitting is FModel || fitting is YModel) continue;
                        if (fitting.Variant.IsTwin) continue;
                    }

                    var role = FlowFromType(owner.Type);
                    if (role != FlowRole.Unknown)
                        return role;

                    foreach (var otherPort in owner.Ports)
                    {
                        var nextNode = otherPort.Node;
                        if (ReferenceEquals(nextNode, node)) continue;
                        if (visited.Add(nextNode))
                            queue.Enqueue(nextNode);
                    }
                }
            }

            return FlowRole.Unknown;
        }
    }
}