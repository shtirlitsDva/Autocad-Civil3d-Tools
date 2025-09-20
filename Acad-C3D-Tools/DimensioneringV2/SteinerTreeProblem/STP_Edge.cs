using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.SteinerTreeProblem
{
    struct STP_Edge
    {
        public STP_Node Source { get; }
        public STP_Node Target { get; }
        public int Weight { get; }
        public STP_Edge(STP_Node source, STP_Node target, int weight)
        {
            Source = source;
            Target = target;
            Weight = weight;
        }
    }
}
