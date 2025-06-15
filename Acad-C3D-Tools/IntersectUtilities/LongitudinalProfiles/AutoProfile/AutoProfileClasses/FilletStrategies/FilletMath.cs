using Autodesk.AutoCAD.Geometry;

using System;
using System.Globalization;
using System.Text;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal static class FilletMath
    {
        private const double Eps = 1e-9;

        internal static bool TryConstructFillet(
            Point2d v,           // common vertex
            Vector2d d1,          // unit dir away from v along seg‑1
            Vector2d d2,          // unit dir away from v along seg‑2
            double r,
            out Point2d p1,        // tangent on seg‑1
            out Point2d p2,        // tangent on seg‑2
            out CircularArc2d fil) // resulting fillet arc
        {
            p1 = p2 = default;
            fil = null;

            double ang = d1.GetAngleTo(d2);
            if (ang < Eps || ang > Math.PI - Eps) return false;

            double t = r / Math.Tan(ang / 2.0);
            p1 = v + d1 * t;
            p2 = v + d2 * t;

            Vector2d bis = (d1 + d2).GetNormal();
            double off = r / Math.Sin(ang / 2.0);
            Point2d cen = v + bis * off;

            double a1CCW = NormCCW(Math.Atan2(p1.Y - cen.Y, p1.X - cen.X));
            double a2CCW = NormCCW(Math.Atan2(p2.Y - cen.Y, p2.X - cen.X));

            bool cw = ((p1 - cen).X * (p2 - cen).Y -
                       (p1 - cen).Y * (p2 - cen).X) < 0.0;

            double startAng, endAng;
            if (cw)
            {
                // convert to clockwise measurement
                startAng = 2 * Math.PI - a1CCW;
                endAng = 2 * Math.PI - a2CCW;
            }
            else
            {
                startAng = a1CCW;
                endAng = a2CCW;
            }

            // enforce end > start as required by AcGe
            if (endAng <= startAng) endAng += 2 * Math.PI;

            fil = new CircularArc2d(cen, r, startAng, endAng, Vector2d.XAxis, cw);
            return true;
        }

        /// <summary>Normalises an angle to the interval 0 ≤ α &lt; 2π (CCW convention).</summary>
        internal static double NormCCW(double a)
        {
            a %= 2 * Math.PI;
            return a < 0 ? a + 2 * Math.PI : a;
        }

        /// <summary>Linear run-up needed on each leg for the given radius and interior corner angle.</summary>
        internal static double RunUp(double radius, double interiorAngle) =>
            radius / Math.Tan(interiorAngle / 2.0);

        /// Returns the 0‑or‑2 possible fillet centres between two circles.
        internal static bool TryCircleCircleTangent(
            Point2d c1, double r1,
            Point2d c2, double r2,
            double filletR,
            out Point2d cen1, out Point2d cen2)
        {
            cen1 = cen2 = default;

            // decide sign: outside fillet → use (R1 + r), inside → |R1 – r|
            double R1 = r1 + filletR;
            double R2 = r2 + filletR;

            Vector2d diff = c2 - c1;
            double d = diff.Length;

            if (d < 1e-9) return false;                      // concentric arcs

            // intersection of two circles (pow‑pow formulation)
            double x = (d * d + R1 * R1 - R2 * R2) / (2 * d);
            double h2 = R1 * R1 - x * x;
            if (h2 < 0) return false;                        // radii too small/large

            Vector2d baseVec = diff.GetNormal() * x;
            Vector2d ortho = new Vector2d(-diff.Y, diff.X).GetNormal() * Math.Sqrt(h2);

            cen1 = c1 + baseVec + ortho;
            cen2 = c1 + baseVec - ortho;
            return true;
        }

        /// Shorten an arc so that its end point becomes <paramref name="p"/>.
        internal static CircularArc2d TrimArcToPoint(CircularArc2d src, Point2d p, bool trimEnd)
        {
            Vector2d radial = (p - src.Center).GetNormal();
            double ang = SignedAngle(src.ReferenceVector, radial);
            if (src.IsClockWise) ang = 2 * Math.PI - ang;

            if (trimEnd)
            {
                double start = src.StartAngle;
                double end = ang;
                if (end <= start) end += 2 * Math.PI;
                return new CircularArc2d(src.Center, src.Radius,
                                         start, end, src.ReferenceVector, src.IsClockWise);
            }
            else
            {
                double start = ang;
                double end = src.EndAngle;
                if (start >= end) start -= 2 * Math.PI;
                return new CircularArc2d(src.Center, src.Radius,
                                         start, end, src.ReferenceVector, src.IsClockWise);
            }
        }

        /// <summary>
        /// Finds a fillet of radius <paramref name="filletR"/> that is tangent to the
        /// finite line segment <paramref name="ln"/> (at its END) and to
        /// <paramref name="arc"/> (at its START).
        /// Returns <c>false</c> if no such fillet exists on either side.
        /// </summary>
        internal static bool TryLineArcTangent(
            LineSegment2d ln,
            CircularArc2d arc,
            double filletR,
            out Point2d centre,
            out Point2d tOnArc,
            out Point2d tOnLine)
        {
            centre = tOnArc = tOnLine = default;

            // fixed input data ---------------------------------------------------
            Point2d v = ln.EndPoint;                         // polyline vertex
            Vector2d u = (ln.EndPoint - ln.StartPoint).GetNormal();   // unit dir of line
            Vector2d n = new Vector2d(-u.Y, u.X).GetNormal();         // unit normal
            Point2d cA = arc.Center;
            double rA = arc.Radius;

            foreach (int sLine in new[] { +1, -1 })               // side of line
                foreach (int sArc in new[] { +1, -1 })               // external (+) or internal (−)
                {
                    double R = (sArc > 0) ? rA + filletR
                                          : Math.Abs(rA - filletR);
                    if (R < 1e-9) continue;                           // degenerate

                    Point2d Q = v + n * (sLine * filletR);           // base point on offset line
                    Vector2d D = Q - cA;
                    double b = 2 * D.DotProduct(u);
                    double c = D.DotProduct(D) - R * R;
                    double disc = b * b - 4 * c;
                    if (disc < 0) continue;

                    double sqrt = Math.Sqrt(disc);
                    foreach (double t in new[] { (-b + sqrt) / 2.0, (-b - sqrt) / 2.0 })
                    {
                        Point2d candC = Q + u * t;

                        // tangent point on arc
                        Vector2d toArc = (candC - cA).GetNormal();
                        Point2d pArc = cA + toArc * rA;
                        if (!arc.IsOn(pArc)) continue;                // must lie on visible span

                        // tangent point on finite line
                        Point2d pLin = candC - n * (sLine * filletR);
                        if (!ln.IsOn(pLin)) continue;                 // must lie on the segment

                        // both points valid → accept
                        centre = candC;
                        tOnArc = pArc;
                        tOnLine = pLin;
                        return true;
                    }
                }
            return false;                                        // no centre fits
        }

        /// <summary>
        /// Signed CCW angle from <paramref name="refVec"/> to <paramref name="vec"/>
        /// in the range 0 ≤ α &lt; 2π.
        /// </summary>
        internal static double SignedAngle(Vector2d refVec, Vector2d vec)
        {
            double cross = refVec.X * vec.Y - refVec.Y * vec.X;   // z‑component
            double dot = refVec.DotProduct(vec);
            double ang = Math.Atan2(cross, dot);                // −π … π
            if (ang < 0) ang += 2 * Math.PI;                      // 0 … 2π
            return ang;
        }
        /// <summary>
        /// Returns <c>true</c> when the circular arc “bulges upward” in WCS, i.e. its
        /// highest point lies above the straight chord connecting the ends.
        /// Works for both CW and CCW arcs, any UCS rotation is ignored (pure XY test).
        /// </summary>
        internal static bool IsArcBulgeUpwards(CircularArc2d arc)
        {
            Point2d s = arc.StartPoint;
            Point2d e = arc.EndPoint;

            // midpoint of the chord
            Point2d midChord = new Point2d((s.X + e.X) * 0.5, (s.Y + e.Y) * 0.5);

            Vector2d rS = (s - arc.Center).GetNormal();
            Vector2d rE = (e - arc.Center).GetNormal();
            Vector2d bis = (rS + rE);
            if (bis.IsZeroLength()) bis = rS.GetPerpendicularVector(); // 180° arc

            Point2d midArc = arc.Center + bis.GetNormal() * arc.Radius;

            // arc bulges upward when its sagitta has a positive global-Y component
            return midArc.Y > midChord.Y;
        }
        /// <summary>
        /// True when the sagitta (maximum deviation from the chord) is smaller than
        /// <paramref name="maxSagitta"/>.  Independent of CW/CCW and ReferenceVector.
        /// </summary>
        internal static bool IsArcAlmostLinear(CircularArc2d arc, double maxSagitta = 0.005)
        {
            Point2d s = arc.StartPoint;
            Point2d e = arc.EndPoint;
            Point2d m = new Point2d((s.X + e.X) * 0.5, (s.Y + e.Y) * 0.5);

            double sagitta = Math.Abs((m - arc.Center).Length - arc.Radius);
            return sagitta <= maxSagitta;
        }

#if DEBUG
        internal static string DumpArcToArcDebug(
            CircularArc2d a1, CircularArc2d a2,
            Point2d v,
            Point2d cA, Point2d cB, Point2d filletCen,
            Vector2d dir1, Vector2d dir2,
            Point2d p1, Point2d p2,
            CircularArc2d trimmed1, CircularArc2d trimmed2)
        {
            Vector2d delta2 = p2 - trimmed2.StartPoint;

            StringBuilder sb = new();
            sb.AppendLine("— Arc-to-Arc fillet diagnostic —");
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "arc1  C({0:F6},{1:F6})  R={2:F6}  CW={3}  SA={4:F6}  EA={5:F6}\n",
                a1.Center.X, a1.Center.Y, a1.Radius, a1.IsClockWise, a1.StartAngle, a1.EndAngle);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "arc2  C({0:F6},{1:F6})  R={2:F6}  CW={3}  SA={4:F6}  EA={5:F6}\n",
                a2.Center.X, a2.Center.Y, a2.Radius, a2.IsClockWise, a2.StartAngle, a2.EndAngle);

            sb.AppendFormat(CultureInfo.InvariantCulture, "vertex v       ({0:F6},{1:F6})\n", v.X, v.Y);
            sb.AppendFormat(CultureInfo.InvariantCulture, "centre cand A  ({0:F6},{1:F6})\n", cA.X, cA.Y);
            sb.AppendFormat(CultureInfo.InvariantCulture, "centre cand B  ({0:F6},{1:F6})\n", cB.X, cB.Y);
            sb.AppendFormat(CultureInfo.InvariantCulture, "chosen centre  ({0:F6},{1:F6})\n", filletCen.X, filletCen.Y);

            sb.AppendFormat(CultureInfo.InvariantCulture,
                "dir1 ({0:F6},{1:F6})  dir2 ({2:F6},{3:F6})\n",
                dir1.X, dir1.Y, dir2.X, dir2.Y);

            sb.AppendFormat(CultureInfo.InvariantCulture,
                "p1  ({0:F6},{1:F6})   p2  ({2:F6},{3:F6})\n",
                p1.X, p1.Y, p2.X, p2.Y);

            sb.AppendFormat(CultureInfo.InvariantCulture,
                "trimmed1.End   ({0:F6},{1:F6})\n",
                trimmed1.EndPoint.X, trimmed1.EndPoint.Y);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "trimmed2.Start ({0:F6},{1:F6})\n",
                trimmed2.StartPoint.X, trimmed2.StartPoint.Y);

            sb.AppendFormat(CultureInfo.InvariantCulture,
                "delta2 = p2-trimmed2.Start ({0:E6},{1:E6})  |δ|={2:E6}\n",
                delta2.X, delta2.Y, delta2.Length);

            return sb.ToString();
        }
        /// <summary>Pretty print of a line-to-arc fillet construction.</summary>
        internal static string DumpLineArcDebug(
            LineSegment2d ln, CircularArc2d arc,
            Point2d v, double filletR,
            Point2d candCentre,
            Point2d pOnLine, Point2d pOnArc,
            FilletFailureReason legTest)
        {
            var sb = new StringBuilder();
            sb.AppendLine("— Line-to-Arc full diagnostic —");
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "line  P0({0:F6},{1:F6})  P1({2:F6},{3:F6})  len={4:F6}\n",
                ln.StartPoint.X, ln.StartPoint.Y,
                ln.EndPoint.X, ln.EndPoint.Y, ln.Length);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "arc   C({0:F6},{1:F6})  R={2:F6}  CW={3}  SA={4:F6}  EA={5:F6}\n",
                arc.Center.X, arc.Center.Y, arc.Radius,
                arc.IsClockWise, arc.StartAngle, arc.EndAngle);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "vertex v        ({0:F6},{1:F6})\n", v.X, v.Y);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "fillet radius   {0:F6}\n", filletR);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "cand centre     ({0:F6},{1:F6})\n", candCentre.X, candCentre.Y);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "pLine           ({0:F6},{1:F6})  onSeg={2}\n",
                pOnLine.X, pOnLine.Y, ln.IsOn(pOnLine));
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "pArc            ({0:F6},{1:F6})  onArc={2}\n",
                pOnArc.X, pOnArc.Y, arc.IsOn(pOnArc));
            sb.AppendFormat("leg-room check  => {0}", legTest);
            return sb.ToString();
        }
#endif
    }
}