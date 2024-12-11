using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.SteinerTreeProblem
{
    struct STP_Terminal
    {
        public STP_Node Node { get; }
        public STP_Terminal(STP_Node node)
        {
            Node = node;
        }
    }
}
