using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphFeatures
{
    internal class UndirectedGraphDto
    {
        public List<NodeJunction> Vertices { get; set; }
        public List<EdgePipeSegment> Edges { get; set; }
    }
}
