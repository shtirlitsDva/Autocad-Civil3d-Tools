using DimensioneringV2.GraphFeatures;

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
        
        public EdgePipeSegment OriginalEdge { get; }

        public PhyEdge(PhyNode source, PhyNode target, EdgePipeSegment edge)
        {
            Source = source;
            Target = target;
            Length = edge.PipeSegment.Length;
            OriginalEdge = edge;
        }

        public PhyNode GetOther(PhyNode PhyNode) =>
            PhyNode == Source ? Target : Source;
    }
}