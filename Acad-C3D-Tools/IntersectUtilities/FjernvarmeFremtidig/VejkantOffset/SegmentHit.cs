using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset
{
    public sealed class SegmentHit
    {
        public ObjectId PolylineId { get; init; }
        public int SegmentIndex { get; init; }
        public Point2d A { get; init; }
        public Point2d B { get; init; }
        public double S0 { get; init; }          // param of A along white line [0..L]
        public double S1 { get; init; }          // param of B along white line [0..L]
        public double Overlap0 { get; init; }    // clamped overlap start [0..L]
        public double Overlap1 { get; init; }    // clamped overlap end   [0..L]
        public double SignedOffset { get; init; }// >0 = left of white dir, <0 = right
        public double SortKey { get; init; }     // for ordering from white start
        public double Offset { get; init; }
    }
}