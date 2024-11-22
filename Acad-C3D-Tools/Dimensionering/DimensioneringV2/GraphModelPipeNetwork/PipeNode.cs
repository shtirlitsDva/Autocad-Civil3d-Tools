using Dimensionering.DimensioneringV2.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dimensionering.DimensioneringV2.GraphModelPipeNetwork
{
    internal class PipeNode
    {
        public Point2D Location { get; }
        public List<PipeSegment> ConnectedSegments { get; }
        public bool IsBuildingConnection { get; } // Indicates if this node is a building connection point

        public PipeNode(Point2D location, bool isBuildingConnection = false)
        {
            Location = location;
            ConnectedSegments = new List<PipeSegment>();
            IsBuildingConnection = isBuildingConnection;
        }
    }
}
