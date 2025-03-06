using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphFeatures
{
    internal class UndirectedGraphDto
    {
        public UndirectedGraphDto() { }
        public UndirectedGraphDto(UndirectedGraph<NodeJunction, EdgePipeSegment> value)
        {
            Vertices = value.Vertices.ToList();
            Edges = value.Edges.ToList();
        }

        public List<NodeJunction> Vertices { get; set; }
        public List<EdgePipeSegment> Edges { get; set; }
    }
}
