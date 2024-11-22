using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dimensionering.DimensioneringV2.GraphModel
{
    internal class KdTreeNode
    {
        public SegmentNode Segment { get; set; }
        public KdTreeNode Left { get; set; }
        public KdTreeNode Right { get; set; }
        public int Depth { get; set; }
    }
}
