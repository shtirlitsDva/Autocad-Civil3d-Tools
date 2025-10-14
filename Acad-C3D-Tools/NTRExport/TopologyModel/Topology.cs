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
        public TPipe(Handle h,
            Func<TPipe, TPort> makeA,
            Func<TPipe, TPort> makeB) : base(h) { A = makeA(this); B = makeB(this); }
        public override IReadOnlyList<TPort> Ports => [A, B];
    }

    internal class TFitting : TElement
    {
        public PipelineElementType Kind { get; }
        private readonly List<TPort> _ports = new();
        public TFitting(Handle h, PipelineElementType k) : base(h) { Kind = k; }
        public void AddPort(TPort p) => _ports.Add(p);
        public override IReadOnlyList<TPort> Ports => _ports;
    }

    internal class Topology
    {
        public List<TNode> Nodes { get; } = new();
        public List<TElement> Elements { get; } = new();
    }
}
