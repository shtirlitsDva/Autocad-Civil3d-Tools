using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.UtilsCommon.Graphs
{
    public class GraphCollection<T> : List<Graph<T>>
    {
        public GraphCollection(IEnumerable<Graph<T>> graphs) : base(graphs)
        {

        }
    }
}
