using DimensioneringV2.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphFeatures
{
    internal class NodeJunction
    {
        public Point2D Location { get; }
        public bool IsRootNode { get; set; } = false;
        public bool IsBuildingNode { get; set; } = false;
        public int Degree { get; set; } = 0;
        public int STP_Node { get; set; } = -1;
        public NodeJunction(Point2D location)
        {
            Location = location;
        }
    }
}
