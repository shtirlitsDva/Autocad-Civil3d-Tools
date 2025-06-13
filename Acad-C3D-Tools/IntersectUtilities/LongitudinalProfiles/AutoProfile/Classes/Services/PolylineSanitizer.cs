using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal static class PolylineSanitizer
    {
        /// <summary>Removes every segment whose <see cref="IPolylineSegment.Length"/>
        /// is shorter than <paramref name="minLen"/> and stretches its neighbours so
        /// the polyline remains continuous.</summary>
        internal static void PruneShortSegments(LinkedList<IPolylineSegment> segments,
                                                double minLen = 0.01)
        {
            for (var node = segments.First; node != null;)
            {
                var nextNode = node.Next;

                if (node.Value.Length < minLen)
                {
                    var prev = node.Previous;
                    var next = node.Next;

                    if (prev != null)
                        prev.Value = ReplaceEndPoint(prev.Value,
                            next?.Value.StartPoint ?? prev.Value.EndPoint);

                    if (next != null)
                        next.Value = ReplaceStartPoint(next.Value,
                            prev?.Value.EndPoint ?? next.Value.StartPoint);

                    segments.Remove(node);
                }

                node = nextNode;
            }
        }

        private static double AngleRelToRef(Point2d p, Point2d c, Vector2d refVec)
        {
            var v = (p - c).GetNormal();
            return refVec.GetAngleTo(v);
        }

        private static IPolylineSegment ReplaceStartPoint(IPolylineSegment seg, Point2d newStart)
        {
            switch (seg)
            {
                case PolylineLineSegment ln:
                    var l = (LineSegment2d)ln.GetGeometry2d();
                    return new PolylineLineSegment(new LineSegment2d(newStart, l.EndPoint));

                case PolylineArcSegment ar:
                    var a = (CircularArc2d)ar.GetGeometry2d();
                    double ns = AngleRelToRef(newStart, a.Center, a.ReferenceVector);
                    double end = a.EndAngle;
                    if (a.IsClockWise)
                    {
                        // convert to clockwise measure and keep end>start
                        ns = 2 * Math.PI - ns;
                        if (end <= ns) end += 2 * Math.PI;
                    }
                    else
                    {
                        if (end <= ns) end += 2 * Math.PI;
                    }
                    var na = new CircularArc2d(a.Center, a.Radius,
                                               ns, end,
                                               a.ReferenceVector,
                                               a.IsClockWise);
                    return new PolylineArcSegment(na);

                default: throw new NotSupportedException();
            }
        }

        private static IPolylineSegment ReplaceEndPoint(IPolylineSegment seg, Point2d newEnd)
        {
            switch (seg)
            {
                case PolylineLineSegment ln:
                    var l = (LineSegment2d)ln.GetGeometry2d();
                    return new PolylineLineSegment(new LineSegment2d(l.StartPoint, newEnd));

                case PolylineArcSegment ar:
                    var a = (CircularArc2d)ar.GetGeometry2d();
                    double ne = AngleRelToRef(newEnd, a.Center, a.ReferenceVector);
                    double start = a.StartAngle;
                    if (a.IsClockWise)
                    {
                        ne = 2 * Math.PI - ne;
                        if (ne <= start) ne += 2 * Math.PI;
                    }
                    else
                    {
                        if (ne <= start) ne += 2 * Math.PI;
                    }
                    var na = new CircularArc2d(a.Center, a.Radius,
                                               start, ne,
                                               a.ReferenceVector,
                                               a.IsClockWise);
                    return new PolylineArcSegment(na);

                default: throw new NotSupportedException();
            }
        }
    }
}