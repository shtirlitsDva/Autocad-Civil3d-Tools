using DimensioneringV2.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphFeatures
{
    internal class JunctionNode
    {
        public Point2D Location { get; }
        public bool IsRootNode { get; set; } = false;
        public int Degree { get; set; } = 0;
        public JunctionNode(Point2D location)
        {
            Location = location;
        }
    }
}
