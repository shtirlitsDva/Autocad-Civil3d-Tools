using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.PhysarumAlgorithm
{
    internal class PhyEdge : IEdge<PhyNode>
    {
        public PhyNode Source { get; }
        public PhyNode Target { get; }

        public double Length { get; set; }
        public double Conductance { get; set; } = 0.01;
        public double Flow { get; set; } = 0.0;

        public string PipeSize { get; set; } = null;
        public double UnitCost { get; set; } = 0.0;

        public double Cost => UnitCost * Length;

        public PhyEdge(PhyNode source, PhyNode target, double length)
        {
            Source = source;
            Target = target;
            Length = length;
        }

        public PhyNode GetOther(PhyNode PhyNode) =>
            PhyNode == Source ? Target : Source;
    }
}