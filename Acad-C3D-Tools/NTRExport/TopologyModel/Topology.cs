using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRExport.TopologyModel
{
    internal enum TFlowRole { Unknown, Supply, Return }
    internal class TNode
    {
        public Pt2 Pos { get; init; }
        public string Name { get; set; } = ""; // assigned later
        public List<TPort> Ports { get; } = new();
    }

    internal class TPort
    {
        public PortRole Role { get; init; }
        public TNode Node { get; init; }
        public TElement Owner { get; init; }
        public TPort(PortRole role, TNode node, TElement owner) { Role = role; Node = node; Owner = owner; }
    }

    internal abstract class TElement
    {
        public Handle Source { get; }
        protected TElement(Handle src) { Source = src; }
        public abstract IReadOnlyList<TPort> Ports { get; }
    }

    internal class TPipe : TElement
    {
        public TPort A { get; }
        public TPort B { get; }
        public int Dn { get; set; } = 0;
        public string? Material { get; set; }
        public IPipeVariant Variant { get; set; } = new SingleVariant();
        public TFlowRole Flow { get; set; } = TFlowRole.Unknown;
        // Cushion spans along this pipe in meters (s0,s1) from A→B
        public List<(double s0, double s1)> CushionSpans { get; } = new();
        public double Length
        {
            get
            {
                var dx = B.Node.Pos.X - A.Node.Pos.X;
                var dy = B.Node.Pos.Y - A.Node.Pos.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }
        }
        public TPipe(Handle h,
            Func<TPipe, TPort> makeA,
            Func<TPipe, TPort> makeB) : base(h) { A = makeA(this); B = makeB(this); }
        public override IReadOnlyList<TPort> Ports => [A, B];
    }

    internal interface IPipeVariant
    {
        string DnSuffix { get; }
        bool IsTwin { get; }
    }
    internal sealed class SingleVariant : IPipeVariant
    {
        public string DnSuffix => "s";
        public bool IsTwin => false;
    }
    internal sealed class TwinVariant : IPipeVariant
    {
        public string DnSuffix => "t";
        public bool IsTwin => true;
    }

    internal class TFitting : TElement
    {
        public PipelineElementType Kind { get; }
        private readonly List<TPort> _ports = new();
        public TFitting(Handle h, PipelineElementType k) : base(h) { Kind = k; }
        public void AddPort(TPort p) => _ports.Add(p);
        public override IReadOnlyList<TPort> Ports => _ports;
    }

    internal sealed class TBendFitting : TFitting
    {
        public Pt2 TangentPoint { get; }
        public TBendFitting(Handle h, PipelineElementType k, Pt2 t) : base(h, k) { TangentPoint = t; }
    }

    internal class Topology
    {
        public List<TNode> Nodes { get; } = new();
        public List<TElement> Elements { get; } = new();
    }
}
