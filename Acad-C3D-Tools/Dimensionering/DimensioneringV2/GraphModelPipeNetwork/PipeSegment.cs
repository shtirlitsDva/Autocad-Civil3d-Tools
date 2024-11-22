using Dimensionering.DimensioneringV2.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dimensionering.DimensioneringV2.GraphModelPipeNetwork
{
    internal class PipeSegment
    {
        public PipeNode StartNode { get; }
        public PipeNode EndNode { get; }
        public double Length { get; } // Length of the segment
        public List<Point2D> Geometry { get; } // List of points representing the geometry of the segment

        public PipeSegment(PipeNode startNode, PipeNode endNode, List<Point2D> geometry)
        {
            StartNode = startNode;
            EndNode = endNode;
            Geometry = geometry;
            Length = CalculateLength(geometry);

            // Add this segment to the connected segments of the nodes
            StartNode.ConnectedSegments.Add(this);
            EndNode.ConnectedSegments.Add(this);
        }

        private double CalculateLength(List<Point2D> geometry)
        {
            double length = 0.0;
            for (int i = 0; i < geometry.Count - 1; i++)
            {
                length += Math.Sqrt(geometry[i].DistanceSquaredTo(geometry[i + 1]));
            }
            return length;
        }
    }

}
