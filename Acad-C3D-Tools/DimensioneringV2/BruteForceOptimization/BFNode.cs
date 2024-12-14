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
        public bool IsRootNode { get; }
        public int Degree { get; }
        public int Level { get; set; } // Level in the network hierarchy
        public int STP_Node { get; }
        public NodeJunction OriginalNodeJunction { get; }
        public int ChromosomeIndex { get; internal set; }

        public BFNode(NodeJunction nodeJunction)
        {
            Location = new Point2D(nodeJunction.Location.X, nodeJunction.Location.Y);
            IsRootNode = nodeJunction.IsRootNode;
            Degree = nodeJunction.Degree;
            STP_Node = nodeJunction.STP_Node;
            OriginalNodeJunction = nodeJunction;
        }
    }
}
