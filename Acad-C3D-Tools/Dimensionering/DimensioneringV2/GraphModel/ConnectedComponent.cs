using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dimensionering.DimensioneringV2.GraphModelRoads
{
    internal class ConnectedComponent
    {
        public List<SegmentNode> Segments { get; }
        public SegmentNode RootNode { get; set; } // Assigned during root mapping

        public ConnectedComponent()
        {
            Segments = new List<SegmentNode>();
        }
    }
}
