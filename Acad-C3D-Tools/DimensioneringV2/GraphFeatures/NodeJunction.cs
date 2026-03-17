using DimensioneringV2.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphFeatures
{
    internal class  NodeJunction
    {
        public Point2D Location { get; }
        public bool IsRootNode { get; set; } = false;
        public bool IsBuildingNode { get; set; } = false;
        public int Degree { get; set; } = 0;
        public int STP_Node { get; set; } = -1;
        public int NodeId { get; set; } = -1;
        public string Name { get; set; } = "";
        public NodeJunction(Point2D location)
        {
            Location = location;
        }
        public override string ToString()
        {
            return Name;
        }
        internal NodeJunction Clone()
        {
            return new NodeJunction(Location)
            {
                IsRootNode = IsRootNode,
                IsBuildingNode = IsBuildingNode,
                Degree = Degree,
                STP_Node = STP_Node,
                NodeId = NodeId,
                Name = Name,
            };
        }
    }
}
