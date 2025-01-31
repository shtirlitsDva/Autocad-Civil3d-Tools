using DimensioneringV2.Geometry;
using DimensioneringV2.GraphFeatures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.BruteForceOptimization
{
    internal class BFNode
    {
        public Point2D Location { get; }
        public bool IsRootNode { get => OriginalNodeJunction.IsRootNode; }
        public bool IsBuildingNode { get => OriginalNodeJunction.IsBuildingNode; }
        public int Degree { get => OriginalNodeJunction.Degree; }
        public int Level { get; set; } // Level in the network hierarchy
        public int STP_Node { get => OriginalNodeJunction.STP_Node; }
        public NodeJunction OriginalNodeJunction { get; }
        
        public BFNode(NodeJunction nodeJunction)
        {
            Location = new Point2D(nodeJunction.Location.X, nodeJunction.Location.Y);
            OriginalNodeJunction = nodeJunction;
        }
    }
}
