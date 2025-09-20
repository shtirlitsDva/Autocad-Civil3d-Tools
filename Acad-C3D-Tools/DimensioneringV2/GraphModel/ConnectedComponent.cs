using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphModelRoads
{
    internal class ConnectedComponent
    {
        public List<SegmentNode> Segments { get; }
        public SegmentNode RootNode { get; set; } // Assigned during root mapping

        public ConnectedComponent()
        {
            Segments = new List<SegmentNode>();
        }

        public List<Point3d> AllPoints()
        {
            List<Point3d> points = new List<Point3d>();
            foreach (var segment in Segments)
            {
                points.Add(segment.StartPoint.To3d());
                points.Add(segment.EndPoint.To3d());
            }
            return points;
        }
    }
}
