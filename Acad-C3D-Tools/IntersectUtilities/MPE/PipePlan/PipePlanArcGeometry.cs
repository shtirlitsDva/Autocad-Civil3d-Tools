using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlan;

/// <summary>
/// Pure 2D arc/tangent math ported from the Norsyn District Heating (NDH) plugin's
/// <c>Norsyn::Geometry::GeometryMath</c> (NorsynDrawingTools/src/NorsynCore/Geometry).
/// NDH draws pipelines as a G1 tangent-continuous chain of line/arc segments; PPDraw
/// derives geometry from corner points + fixed fillets. These functions are the
/// model-agnostic core that lets PPDraw represent and re-solve genuine arc↔arc and
/// arc↔line tangent junctions instead of only symmetric line-arc-line fillets.
///
/// Bulge convention matches AutoCAD polylines: bulge = tan(sweep/4); positive = CCW
/// (curves left), negative = CW (curves right).
/// </summary>
internal static class PipePlanArcGeometry
{
    private const double DirEps = 1e-9;
    private const double AngleTol = 1e-6;
    private const double SideTol = 1e-9;

    /// <summary>Circle backing an arc segment: center, radius, orientation and the
    /// swept angle from arc start to arc end (always in (0, 2π)).</summary>
    internal readonly record struct ArcCircle(Point2d Center, double Radius, bool IsCcw, double Sweep);

    /// <summary>Result of re-solving the previous arc when a straight leg is drawn off
    /// it. <see cref="ArcEnd"/>/<see cref="ArcBulge"/> replace the arc's end vertex and
    /// bulge so the departing line at <see cref="LineEnd"/> leaves tangent to the arc.
    /// When <see cref="RemoveArcOnCommit"/> is set the arc collapsed to a point and
    /// should be dropped on commit; when <see cref="PointAdditionAllowed"/> is false the
    /// cursor is not a legal endpoint this tick (no tangent line exists).</summary>
    internal readonly record struct LineAfterArcResult(
        Point2d ArcEnd,
        double ArcBulge,
        Point2d LineEnd,
        bool PointAdditionAllowed,
        bool RemoveArcOnCommit);

    private static double Cross(Vector2d a, Vector2d b) => (a.X * b.Y) - (a.Y * b.X);

    private static Vector2d NormalizeOrZero(Vector2d v)
    {
        double len = v.Length;
        return len < DirEps ? new Vector2d(0.0, 0.0) : v / len;
    }

    public static bool IsArcBulge(double bulge, double eps = 1e-9) => Math.Abs(bulge) > eps;

    /// <summary>CCW angle in [0, 2π) from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static double CcwAngleBetween(Vector2d from, Vector2d to)
    {
        double angle = Math.Atan2(Cross(from, to), from.DotProduct(to));
        if (angle < 0.0)
        {
            angle += 2.0 * Math.PI;
        }
        return angle;
    }

    public static double ArcRadius(Point2d p1, Point2d p2, double bulge)
    {
        if (!IsArcBulge(bulge))
        {
            return double.MaxValue;
        }
        double chord = p1.GetDistanceTo(p2);
        return chord * (1.0 + bulge * bulge) / (4.0 * Math.Abs(bulge));
    }

    public static Point2d ArcCenter(Point2d p1, Point2d p2, double bulge)
    {
        Point2d midpoint = new((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0);

        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double chord = Math.Sqrt(dx * dx + dy * dy);
        if (chord < 1e-10)
        {
            return midpoint;
        }

        double perpX = -dy / chord;
        double perpY = dx / chord;

        double sagitta = chord / 2.0 * Math.Abs(bulge);
        double radius = ArcRadius(p1, p2, bulge);
        double apothem = radius - sagitta;

        // AutoCAD bulge convention: positive bulge (CCW) places the centre to the LEFT of
        // p1->p2. (NDH's source math uses the opposite sign; these functions are adapted to
        // AutoCAD's polyline convention so they compose with AutoCAD bulges directly.)
        if (bulge < 0.0)
        {
            perpX = -perpX;
            perpY = -perpY;
        }

        return new Point2d(midpoint.X + (perpX * apothem), midpoint.Y + (perpY * apothem));
    }

    /// <summary>Unit tangent at the arc/line start, pointing along the segment.</summary>
    public static Vector2d TangentAtStart(Point2d p1, Point2d p2, double bulge)
    {
        if (!IsArcBulge(bulge))
        {
            return NormalizeOrZero(p2 - p1);
        }

        Point2d center = ArcCenter(p1, p2, bulge);
        Vector2d radiusVec = new(p1.X - center.X, p1.Y - center.Y);
        // AutoCAD convention (positive bulge = CCW): tangent is the inward radius rotated
        // +90° for CCW, -90° for CW.
        Vector2d tangent = bulge > 0.0
            ? new Vector2d(-radiusVec.Y, radiusVec.X)
            : new Vector2d(radiusVec.Y, -radiusVec.X);
        return NormalizeOrZero(tangent);
    }

    /// <summary>Unit tangent at the arc/line end, pointing along the segment.</summary>
    public static Vector2d TangentAtEnd(Point2d p1, Point2d p2, double bulge)
    {
        if (!IsArcBulge(bulge))
        {
            return NormalizeOrZero(p2 - p1);
        }

        Point2d center = ArcCenter(p1, p2, bulge);
        Vector2d radiusVec = new(p2.X - center.X, p2.Y - center.Y);
        // AutoCAD convention (positive bulge = CCW): tangent is the outward radius rotated
        // +90° for CCW, -90° for CW.
        Vector2d tangent = bulge > 0.0
            ? new Vector2d(-radiusVec.Y, radiusVec.X)
            : new Vector2d(radiusVec.Y, -radiusVec.X);
        return NormalizeOrZero(tangent);
    }

    /// <summary>Bulge of the unique arc from <paramref name="start"/> to
    /// <paramref name="end"/> that leaves tangent to <paramref name="tangentAtStart"/>.
    /// Returns 0 (straight) for a degenerate/colinear case. This is the core of
    /// tangent-continuous arc drawing (NDH TangentArcState).</summary>
    public static double BulgeFromStartTangent(Point2d start, Point2d end, Vector2d tangentAtStart)
    {
        Vector2d chord = end - start;
        double chordLength = chord.Length;
        if (chordLength < 1e-9)
        {
            return 0.0;
        }

        Vector2d nChord = chord / chordLength;
        Vector2d nTangent = NormalizeOrZero(tangentAtStart);

        double dot = Math.Clamp(nChord.DotProduct(nTangent), -1.0, 1.0);
        double halfIncluded = Math.Acos(dot);
        if (halfIncluded < 1e-9)
        {
            return 0.0;
        }

        double cross = Cross(nChord, nTangent);
        double bulge = Math.Tan(halfIncluded / 2.0);
        return cross < 0.0 ? -bulge : bulge;
    }

    /// <summary>Bulge of the unique arc from <paramref name="start"/> to
    /// <paramref name="end"/> that arrives tangent to <paramref name="tangentAtEnd"/>.</summary>
    public static double BulgeFromEndTangent(Point2d start, Point2d end, Vector2d tangentAtEnd)
    {
        Vector2d reversed = tangentAtEnd.Negate();
        double reversedBulge = BulgeFromStartTangent(end, start, reversed);
        return -reversedBulge;
    }

    /// <summary>Bulge of the arc from <paramref name="start"/> to <paramref name="end"/>
    /// that lies on the circle centred at <paramref name="center"/> with the given
    /// orientation. Returns 0 for a degenerate sweep.</summary>
    public static double BulgeFromArcCenter(Point2d center, Point2d start, Point2d end, bool isCcw)
    {
        Vector2d v1 = start - center;
        Vector2d v2 = end - center;
        if (v1.Length < 1e-9 || v2.Length < 1e-9)
        {
            return 0.0;
        }

        double angle = CcwAngleBetween(v1, v2);
        double twoPi = 2.0 * Math.PI;
        if (angle < 1e-9 || angle > twoPi - 1e-9)
        {
            return 0.0;
        }

        double sweep = isCcw ? angle : twoPi - angle;
        if (sweep < 1e-9 || sweep > twoPi - 1e-9)
        {
            return 0.0;
        }

        double bulge = Math.Tan(sweep / 4.0);
        return isCcw ? -bulge : bulge;
    }

    /// <summary>Backing circle of the arc defined by start/end/bulge. Returns false for a
    /// straight bulge or a degenerate (near-0 / near-2π) sweep.</summary>
    public static bool TryArcCircleFromBulge(Point2d start, Point2d end, double bulge, out ArcCircle circle)
    {
        circle = default;
        if (!IsArcBulge(bulge))
        {
            return false;
        }

        Point2d center = ArcCenter(start, end, bulge);
        double radius = ArcRadius(start, end, bulge);
        bool isCcw = bulge < 0.0;

        Vector2d vStart = start - center;
        Vector2d vEnd = end - center;
        double angle = CcwAngleBetween(vStart, vEnd);
        double twoPi = 2.0 * Math.PI;
        double sweep = isCcw ? angle : twoPi - angle;
        if (!SweepValid(sweep))
        {
            return false;
        }

        circle = new ArcCircle(center, radius, isCcw, sweep);
        return true;
    }

    private static bool SweepValid(double sweep) => sweep > AngleTol && sweep < (2.0 * Math.PI - AngleTol);

    private static double SweepFromStart(ArcCircle circle, Point2d start, Point2d point)
    {
        Vector2d vStart = start - circle.Center;
        Vector2d vPoint = point - circle.Center;
        double angle = CcwAngleBetween(vStart, vPoint);
        double twoPi = 2.0 * Math.PI;
        return circle.IsCcw ? angle : twoPi - angle;
    }

    /// <summary>The two points on the circle where a tangent line from
    /// <paramref name="externalPoint"/> touches it. Returns false if the point is
    /// strictly inside the circle. When the point is on the circle both outputs equal it.</summary>
    public static bool TryExternalTangentPoints(Point2d externalPoint, Point2d center, double radius, out Point2d t1, out Point2d t2)
    {
        const double tolerance = 1e-9;
        t1 = default;
        t2 = default;

        double dx = externalPoint.X - center.X;
        double dy = externalPoint.Y - center.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance < radius - tolerance)
        {
            return false;
        }

        if (Math.Abs(distance - radius) <= tolerance)
        {
            t1 = externalPoint;
            t2 = externalPoint;
            return true;
        }

        double ratio = Math.Clamp(radius / distance, 0.0, 1.0);
        double alpha = Math.Asin(ratio);
        double angleCp = Math.Atan2(dy, dx);
        double offset = (Math.PI / 2.0) - alpha;

        double angle1 = angleCp + offset;
        double angle2 = angleCp - offset;

        t1 = new Point2d(center.X + (Math.Cos(angle1) * radius), center.Y + (Math.Sin(angle1) * radius));
        t2 = new Point2d(center.X + (Math.Cos(angle2) * radius), center.Y + (Math.Sin(angle2) * radius));
        return true;
    }

    /// <summary>
    /// Re-solves the previous arc when a straight leg is drawn off its end toward
    /// <paramref name="cursor"/>, so that the departing line is tangent to the arc.
    /// Direct port of NDH <c>solveLineAfterArc</c>. The arc is trimmed (or extended,
    /// or collapsed) to the tangent point that requires the least arc extension, biased
    /// toward <paramref name="lastTangent"/> on ties.
    ///
    /// NOT YET WIRED IN (Stage 4). This and its helpers (<see cref="TryArcCircleFromBulge"/>,
    /// <see cref="BulgeFromArcCenter"/>, the internal isCcw/sweep logic) still carry NDH's
    /// sign convention, whereas <see cref="ArcCenter"/>/<see cref="TangentAtStart"/>/
    /// <see cref="TangentAtEnd"/> were converted to AutoCAD's. Reconcile the whole
    /// isCcw/sweep chain to AutoCAD convention and re-verify against AutoCAD's own arc
    /// derivatives before enabling the tangent-snap path.
    /// </summary>
    public static LineAfterArcResult SolveLineAfterArc(
        Point2d arcStart,
        Point2d arcEnd,
        double arcBulge,
        ArcCircle circle,
        Point2d cursor,
        Vector2d lastTangent)
    {
        LineAfterArcResult Result(Point2d aEnd, double aBulge, Point2d lEnd, bool add, bool remove)
            => new(aEnd, aBulge, lEnd, add, remove);

        Vector2d forward = NormalizeOrZero(TangentAtStart(arcStart, arcEnd, arcBulge));
        if (forward.Length < DirEps)
        {
            return Result(arcEnd, arcBulge, cursor, true, false);
        }

        if (!TryExternalTangentPoints(cursor, circle.Center, circle.Radius, out Point2d t1, out Point2d t2))
        {
            return Result(arcEnd, arcBulge, cursor, false, false);
        }

        Vector2d toCursor = cursor - arcStart;
        double ahead = toCursor.DotProduct(forward);
        double side = Cross(forward, toCursor);
        if (!circle.IsCcw)
        {
            side = -side;
        }

        Point2d ProjectAlong(Point2d basePoint, Point2d target, Vector2d dir, out bool ok)
        {
            ok = false;
            Vector2d d = NormalizeOrZero(dir);
            if (d.Length < DirEps)
            {
                return basePoint;
            }
            double projLen = (target - basePoint).DotProduct(d);
            if (projLen <= 0.0)
            {
                return basePoint;
            }
            ok = true;
            return basePoint + (d * projLen);
        }

        if (side < -SideTol)
        {
            if (ahead > 0.0)
            {
                Point2d projected = ProjectAlong(arcStart, cursor, forward, out bool ok);
                return Result(arcStart, 0.0, ok ? projected : cursor, true, false);
            }

            return Result(arcStart, 0.0, arcStart, true, true);
        }

        double twoPi = 2.0 * Math.PI;
        double s0 = circle.Sweep;

        Vector2d TangentAtPoint(Point2d point)
        {
            Vector2d radiusVec = point - circle.Center;
            Vector2d tangent = circle.IsCcw
                ? new Vector2d(-radiusVec.Y, radiusVec.X)
                : new Vector2d(radiusVec.Y, -radiusVec.X);
            return NormalizeOrZero(tangent);
        }

        (Point2d point, double extension, bool valid) MakeCandidate(Point2d point)
        {
            double sweep = SweepFromStart(circle, arcStart, point);
            if (!SweepValid(sweep))
            {
                return (point, 0.0, false);
            }

            double extension = sweep - s0;
            if (extension < 0.0)
            {
                extension += twoPi;
            }

            Vector2d lineDir = NormalizeOrZero(cursor - point);
            if (lineDir.Length < DirEps)
            {
                return (point, 0.0, false);
            }

            Vector2d tangentDir = TangentAtPoint(point);
            if (tangentDir.Length < DirEps)
            {
                return (point, 0.0, false);
            }

            if (lineDir.DotProduct(tangentDir) <= DirEps)
            {
                return (point, 0.0, false);
            }

            return (point, extension, true);
        }

        LineAfterArcResult ClampToStart()
        {
            Point2d projected = ProjectAlong(arcStart, cursor, forward, out bool ok);
            return ok
                ? Result(arcStart, 0.0, projected, true, false)
                : Result(arcStart, 0.0, arcStart, true, true);
        }

        var c1 = MakeCandidate(t1);
        var c2 = MakeCandidate(t2);

        if (!c1.valid && !c2.valid)
        {
            return ClampToStart();
        }

        Point2d chosen = c1.valid ? c1.point : c2.point;
        if (c1.valid && c2.valid)
        {
            if (Math.Abs(c1.extension - c2.extension) > AngleTol)
            {
                chosen = c1.extension < c2.extension ? c1.point : c2.point;
            }
            else
            {
                Vector2d lt = NormalizeOrZero(lastTangent);
                if (lt.Length < DirEps)
                {
                    chosen = c1.point;
                }
                else
                {
                    Vector2d dir1 = NormalizeOrZero(cursor - c1.point);
                    Vector2d dir2 = NormalizeOrZero(cursor - c2.point);
                    chosen = dir1.DotProduct(lt) >= dir2.DotProduct(lt) ? c1.point : c2.point;
                }
            }
        }

        double newBulge = BulgeFromArcCenter(circle.Center, arcStart, chosen, circle.IsCcw);
        if (!IsArcBulge(newBulge) || !SweepValid(SweepFromStart(circle, arcStart, chosen)))
        {
            return ClampToStart();
        }

        return Result(chosen, newBulge, cursor, true, false);
    }

    /// <summary>
    /// Collapses coincident vertices and merges colinear straight runs in place.
    /// Port of NDH <c>simplifyPolyline</c>. <paramref name="vertices"/> and
    /// <paramref name="bulges"/> are aligned (bulge[i] applies to segment i→i+1);
    /// the final vertex's bulge is unused.
    /// </summary>
    public static void SimplifyPolyline(
        List<Point2d> vertices,
        List<double> bulges,
        double pointTolerance = 1e-6,
        double angleTolerance = 1e-6)
    {
        if (vertices.Count < 2)
        {
            return;
        }

        if (bulges.Count < vertices.Count)
        {
            while (bulges.Count < vertices.Count)
            {
                bulges.Add(0.0);
            }
        }

        int i = 0;
        while (i + 1 < vertices.Count)
        {
            if (vertices[i].GetDistanceTo(vertices[i + 1]) <= pointTolerance)
            {
                vertices.RemoveAt(i + 1);
                if (i < bulges.Count)
                {
                    bulges.RemoveAt(i);
                }
                while (bulges.Count < vertices.Count)
                {
                    bulges.Add(0.0);
                }
                if (i > 0)
                {
                    i--;
                }
                continue;
            }
            i++;
        }

        i = 0;
        while (i + 2 < vertices.Count)
        {
            if (i + 1 >= bulges.Count)
            {
                break;
            }

            double b0 = bulges[i];
            double b1 = (i + 1 < bulges.Count) ? bulges[i + 1] : 0.0;

            if (!IsArcBulge(b0) && !IsArcBulge(b1))
            {
                Vector2d d1 = vertices[i + 1] - vertices[i];
                Vector2d d2 = vertices[i + 2] - vertices[i + 1];
                double len1 = d1.Length;
                double len2 = d2.Length;

                if (len1 > pointTolerance && len2 > pointTolerance)
                {
                    double sinVal = Math.Abs(Cross(d1, d2)) / (len1 * len2);
                    double cosVal = d1.DotProduct(d2) / (len1 * len2);

                    if (sinVal <= angleTolerance && cosVal > 0.0)
                    {
                        vertices.RemoveAt(i + 1);
                        if (i + 1 < bulges.Count)
                        {
                            bulges.RemoveAt(i + 1);
                        }
                        if (i < bulges.Count)
                        {
                            bulges[i] = 0.0;
                        }
                        while (bulges.Count < vertices.Count)
                        {
                            bulges.Add(0.0);
                        }
                        if (i > 0)
                        {
                            i--;
                        }
                        continue;
                    }
                }
            }

            i++;
        }

        if (bulges.Count > vertices.Count)
        {
            bulges.RemoveRange(vertices.Count, bulges.Count - vertices.Count);
        }
    }
}
