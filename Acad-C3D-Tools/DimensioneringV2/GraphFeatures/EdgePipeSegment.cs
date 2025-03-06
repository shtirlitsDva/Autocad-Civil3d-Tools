using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphFeatures
{
    internal class EdgePipeSegment : IEdge<NodeJunction>
    {
        public AnalysisFeature PipeSegment { get; set; }
        public int Level { get; set; } // Level in the network hierarchy

        private NodeJunction _source;
        public NodeJunction Source { get => _source; set => _source = value; }
        private NodeJunction _target;
        public NodeJunction Target { get => _target; set => _target = value; }

        public EdgePipeSegment() { }

        public EdgePipeSegment(
            NodeJunction source, 
            NodeJunction target, 
            AnalysisFeature pipeSegment)
        {
            Source = source;
            Target = target;
            PipeSegment = pipeSegment;
            Level = -1; // Default value, will be set during level calculation
        }
    }
}
