using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal sealed class TriageStrategyLineArcS1Short : ITriageStrategy
    {
        private readonly FilletStrategyLineToArc _lineToArc = new();
        private readonly FilletStrategyArcToArc _arcToArc = new();

        public bool CanHandle(IFilletStrategy fs, FilletFailureReason fr) =>
            fs is FilletStrategyLineToArc &&
            (fr == FilletFailureReason.Seg1TooShort ||
            fr == FilletFailureReason.RadiusTooLarge);

        public IFilletResult Triage(
            (LinkedListNode<IPolylineSegment> firstNode,
             LinkedListNode<IPolylineSegment> secondNode) nodes,
            double radius)
        {
            if (nodes.firstNode is null || nodes.secondNode is null)
                throw new ArgumentNullException(nameof(nodes), "Nodes cannot be null.");

            double toGo = 0.0;
            double limit = radius;

            var arcNode = nodes.secondNode;          // the arc that stays fixed
            var candNode = nodes.firstNode.Previous;   // start with *next* segment
            while (candNode != null && toGo < limit)
            {
                switch (candNode.Value)
                {
                    // ---------- try arc → line --------------------------------------
                    case PolylineLineSegment ln:
                        {
                            toGo += ln.Length;

                            var res = _lineToArc.CreateFillet(candNode.Value, arcNode.Value, radius);
                            if (res.Success) return res;

                            // if the only reason is "next line too short" → go on, else fail
                            if (res.FailureReason is FilletFailureReason.Seg1TooShort
                                or FilletFailureReason.RadiusTooLarge)
                            {
                                candNode = candNode.Previous;
                                continue;
                            }
                            return res;
                        }

                    // ---------- upward-bulge arc may be skipped ---------------------
                    case PolylineArcSegment a when
                         FilletMath.IsArcBulgeUpwards((CircularArc2d)a.GetGeometry2d()):
                        candNode = candNode.Previous;
                        continue;

                    case PolylineArcSegment a when
                         FilletMath.IsArcAlmostLinear((CircularArc2d)a.GetGeometry2d(), 0.05):
                        candNode = candNode.Previous;
                        continue;

                    // ---------- downward-bulge arc must fillet ----------------------
                    case PolylineArcSegment _:
                        {
                            //Dont handle addition to toGo here, as it is not important

                            var res = _arcToArc.CreateFillet(candNode.Value, arcNode.Value, radius);
                            return res;       // succeed or fail — we cannot skip further
                        }

                    default:
                        return new FilletResultThreePart()
                        { FailureReason = FilletFailureReason.UnsupportedSegmentTypes };
                }
            }

            // ran out of segments without success
            return new FilletResultThreePart()
            { FailureReason = FilletFailureReason.Seg1TooShort };
        }
    }
}