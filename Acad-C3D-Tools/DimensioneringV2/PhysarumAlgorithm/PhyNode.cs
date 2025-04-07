using DimensioneringV2.Geometry;
using DimensioneringV2.GraphFeatures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.PhysarumAlgorithm
{
    internal class PhyNode
    {        
        public Point2D Location { get; set; }
        public bool IsSource { get; set; }
        public bool IsTerminal { get; set; }
        public double ExternalDemand { get; set; } = 0.0;
        public double Pressure { get; set; } = 0.0;
        public NodeJunction OriginalNodeJunction { get; }

        public PhyNode(NodeJunction nodeJunction)
        {
            Location = new Point2D(nodeJunction.Location.X, nodeJunction.Location.Y);
            OriginalNodeJunction = nodeJunction;
        }        
    }
}
