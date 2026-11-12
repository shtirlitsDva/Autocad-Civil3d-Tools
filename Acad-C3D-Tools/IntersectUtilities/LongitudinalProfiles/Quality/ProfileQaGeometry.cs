using System;
using System.Collections.Generic;
using System.Linq;
using Norsyn.AecPropertySets;

namespace IntersectUtilities.LongitudinalProfiles.Quality;

internal enum QaCurveKind { Line, Ellipse, Parabola }

// All coordinates are actual alignment station and elevation, in metres.
internal readonly record struct QaPoint(double S, double Z);

internal readonly record struct QaBulgeArc(QaPoint Centre, double Radius, double Angle, double Sweep)
{
    internal static Result<QaBulgeArc> FromEndpoints(QaPoint start, QaPoint end, double bulge)
    {
        double dx = end.S - start.S, dy = end.Z - start.Z;
        double chord = Math.Sqrt(dx * dx + dy * dy);
        if (!double.IsFinite(chord) || chord <= 0 || !double.IsFinite(bulge) || bulge == 0)
            return Result<QaBulgeArc>.Failure("Arc endpoints and bulge do not define a finite, nonzero arc.");

        // Work from endpoint differences: native short-arc reconstruction loses radius precision at large coordinates.
        double offset = (1 - bulge * bulge) / (4 * bulge);
        double cx = dx / 2 - dy * offset, cy = dy / 2 + dx * offset;
        double radius = chord * (1 + bulge * bulge) / (4 * Math.Abs(bulge));
        var centre = new QaPoint(start.S + cx, start.Z + cy);
        if (!double.IsFinite(radius) || !double.IsFinite(centre.S) || !double.IsFinite(centre.Z))
            return Result<QaBulgeArc>.Failure("Arc centre or radius is outside the supported numeric range.");
        return Result<QaBulgeArc>.Success(new(centre, radius, Math.Atan2(-cy, -cx), 4 * Math.Atan(bulge)));
    }
}

internal readonly record struct QaInterval(double Start, double End)
{
    internal double Length => End - Start;
}

internal readonly record struct QaSegment(
    double Start, double End, QaCurveKind Kind,
    double A, double B, double C, double D, double E, int Index)
{
    internal static QaSegment Line(QaPoint p, QaPoint q, int index) =>
        new(Math.Min(p.S, q.S), Math.Max(p.S, q.S), QaCurveKind.Line,
            p.Z, (q.Z - p.Z) / (q.S - p.S), p.S, 0, 0, index);
    internal static QaSegment Parabola(double start, double end, double elevation, double grade, double second, int index) =>
        new(start, end, QaCurveKind.Parabola, elevation, grade, start, second / 2, 0, index);
    internal static QaSegment Ellipse(double start, double end, double cx, double cz, double rx, double ry, double sign, int index) =>
        new(start, end, QaCurveKind.Ellipse, cx, cz, rx, ry, sign, index);

    internal double At(double station)
    {
        double t = station - C;
        return Kind switch
        {
            QaCurveKind.Line => A + B * t,
            QaCurveKind.Parabola => A + B * t + D * t * t,
            QaCurveKind.Ellipse => B + E * D * Math.Sqrt(Math.Max(0, 1 - Math.Pow((station - A) / C, 2)))
        };
    }

    internal double Grade(double station)
    {
        double u = (station - A) / C;
        return Kind switch
        {
            QaCurveKind.Line => B,
            QaCurveKind.Parabola => B + 2 * D * (station - C),
            QaCurveKind.Ellipse => -E * D / C * u / Math.Sqrt(Math.Max(1e-30, 1 - u * u))
        };
    }

    internal double Radius(double station)
    {
        double grade = Grade(station);
        double u = (station - A) / C;
        double second = Kind switch
        {
            QaCurveKind.Line => 0,
            QaCurveKind.Parabola => 2 * D,
            QaCurveKind.Ellipse => -E * D / (C * C) / Math.Pow(Math.Max(1e-30, 1 - u * u), 1.5)
        };
        return Math.Abs(second) < 1e-30 ? double.PositiveInfinity : Math.Pow(1 + grade * grade, 1.5) / Math.Abs(second);
    }

    internal (double Radius, double Station) MinimumRadius(double start, double end)
    {
        var candidates = new List<double> { start, end };
        double middle = Kind switch
        {
            QaCurveKind.Line => start,
            QaCurveKind.Parabola => D == 0 ? start : C - B / (2 * D),
            QaCurveKind.Ellipse => A
        };
        if (middle > start && middle < end) candidates.Add(middle);
        var curve = this;
        return candidates.Select(s => (Radius: curve.Radius(s), Station: s)).OrderBy(p => p.Radius).First();
    }

    internal IEnumerable<double> GradeEquals(double grade, double start, double end)
    {
        double station = Kind switch
        {
            QaCurveKind.Line => start,
            QaCurveKind.Parabola => D == 0 ? start : C + (grade - B) / (2 * D),
            QaCurveKind.Ellipse => A - E * C * grade / Math.Sqrt(Math.Pow(D / C, 2) + grade * grade)
        };
        if (station > start && station < end) yield return station;
    }

    // z = P(u) + k*sqrt(Q(u)), u in [0,1]. Local coordinates keep large drawing offsets out of the polynomials.
    internal (double[] P, double K, double[] Q) Expression()
    {
        double length = End - Start;
        double t = Start - C;
        double x = (Start - A) / C;
        double dx = length / C;
        return Kind switch
        {
            QaCurveKind.Line => (new[] { At(Start), B * length }, 0, new[] { 0.0 }),
            QaCurveKind.Parabola => (new[] { At(Start), (B + 2 * D * t) * length, D * length * length }, 0, new[] { 0.0 }),
            QaCurveKind.Ellipse => (new[] { B }, E * D, new[] { 1 - x * x, -2 * x * dx, -dx * dx })
        };
    }
}

internal enum QaEdgeKind { Line, Ellipse }

// A full ellipse, elliptical arc, or straight edge. Closed polyline bulges remain exact conic edges.
internal readonly record struct QaEdge(QaEdgeKind Kind, QaPoint P, QaPoint Q,
    double Cx, double Cz, double Rx, double Ry, double Angle, double Sweep)
{
    internal static QaEdge Line(QaPoint p, QaPoint q) => new(QaEdgeKind.Line, p, q, 0, 0, 0, 0, 0, 0);
    internal static QaEdge Arc(double cx, double cz, double rx, double ry, double angle, double sweep) =>
        new(QaEdgeKind.Ellipse,
            new(cx + rx * Math.Cos(angle), cz + ry * Math.Sin(angle)),
            new(cx + rx * Math.Cos(angle + sweep), cz + ry * Math.Sin(angle + sweep)),
            cx, cz, rx, ry, angle, sweep);

    internal bool OnArc(QaPoint point)
    {
        if (Kind == QaEdgeKind.Line)
            return point.S >= Math.Min(P.S, Q.S) - QaGeometry.LinearTolerance && point.S <= Math.Max(P.S, Q.S) + QaGeometry.LinearTolerance &&
                point.Z >= Math.Min(P.Z, Q.Z) - QaGeometry.LinearTolerance && point.Z <= Math.Max(P.Z, Q.Z) + QaGeometry.LinearTolerance;
        if (Math.Abs(Sweep) >= 2 * Math.PI - 1e-10) return true;
        double angle = Math.Atan2((point.Z - Cz) / Ry, (point.S - Cx) / Rx);
        double delta = QaGeometry.PositiveAngle(Sweep >= 0 ? angle - Angle : Angle - angle);
        return delta <= Math.Abs(Sweep) + 1e-9 || 2 * Math.PI - delta < 1e-9;
    }

    internal double Residual(QaPoint point) => Kind switch
    {
        QaEdgeKind.Line => ((point.S - P.S) * (Q.Z - P.Z) - (point.Z - P.Z) * (Q.S - P.S)) /
            Math.Max(1e-30, Math.Sqrt(Math.Pow(Q.Z - P.Z, 2) + Math.Pow(Q.S - P.S, 2))),
        QaEdgeKind.Ellipse => Math.Pow((point.S - Cx) / Rx, 2) + Math.Pow((point.Z - Cz) / Ry, 2) - 1
    };

    internal double BoundaryDistanceEstimate(QaPoint point) => Kind switch
    {
        QaEdgeKind.Line => Math.Abs(Residual(point)),
        QaEdgeKind.Ellipse => EllipseBoundaryDistanceEstimate(point)
    };

    private double EllipseBoundaryDistanceEstimate(QaPoint point)
    {
        double x = (point.S - Cx) / Rx, y = (point.Z - Cz) / Ry;
        double gradient = 2 * Math.Sqrt(Math.Pow(x / Rx, 2) + Math.Pow(y / Ry, 2));
        // Near the boundary, residual / gradient is normal distance in metres, not a size-dependent normalized error.
        return gradient == 0 ? double.PositiveInfinity : Math.Abs(x * x + y * y - 1) / gradient;
    }

    internal IEnumerable<double> Intersections(QaSegment curve)
    {
        var (p, k, q) = curve.Expression();
        double[] a;
        double[] b;
        double length = curve.End - curve.Start;
        if (Kind == QaEdgeKind.Line)
        {
            double dx = Q.S - P.S, dz = Q.Z - P.Z;
            a = QaPolynomial.Add(new[] { (curve.Start - P.S) * dz, length * dz }, QaPolynomial.Scale(p, -dx));
            a[0] += P.Z * dx;
            b = new[] { -dx * k };
        }
        else
        {
            double[] shifted = (double[])p.Clone(); shifted[0] -= Cz;
            double x = (curve.Start - Cx) / Rx, delta = length / Rx;
            a = QaPolynomial.Add(new[] { x * x - 1, 2 * x * delta, delta * delta },
                QaPolynomial.Scale(QaPolynomial.Add(QaPolynomial.Multiply(shifted, shifted), QaPolynomial.Scale(q, k * k)), 1 / (Ry * Ry)));
            b = QaPolynomial.Scale(shifted, 2 * k / (Ry * Ry));
        }
        double[] equation = k == 0 ? a : QaPolynomial.Add(QaPolynomial.Multiply(a, a), QaPolynomial.Scale(QaPolynomial.Multiply(QaPolynomial.Multiply(b, b), q), -1));
        foreach (double root in QaPolynomial.Roots(equation, 0, 1))
        {
            double station = curve.Start + root * length;
            var point = new QaPoint(station, curve.At(station));
            // Squaring introduces a second branch: test the original equation and the edge's actual extent.
            if (Math.Abs(Residual(point)) < 2e-7 && OnArc(point)) yield return station;
        }
    }
}

internal sealed record QaRegion(IReadOnlyList<QaEdge> Edges)
{
    internal bool Contains(QaPoint point)
    {
        foreach (var edge in Edges)
            if (edge.OnArc(point) && edge.BoundaryDistanceEstimate(point) <= QaGeometry.CrossingContactTolerance) return false;
        // Move an exact vertex-height ray by a numerical epsilon; this is unrelated to engineering clearance tolerance.
        double height = point.Z;
        if (Edges.Any(e => Math.Abs(e.P.Z - height) < 1e-10 || Math.Abs(e.Q.Z - height) < 1e-10)) height += 2e-10;
        int crossings = 0;
        foreach (var edge in Edges)
        {
            if (edge.Kind == QaEdgeKind.Line)
            {
                if ((edge.P.Z > height) == (edge.Q.Z > height)) continue;
                double x = edge.P.S + (height - edge.P.Z) * (edge.Q.S - edge.P.S) / (edge.Q.Z - edge.P.Z);
                if (x > point.S) crossings++;
            }
            else
            {
                double ordinate = (height - edge.Cz) / edge.Ry;
                if (Math.Abs(ordinate) >= 1) continue;
                double first = Math.Asin(ordinate);
                foreach (double angle in new[] { first, Math.PI - first })
                {
                    var hit = new QaPoint(edge.Cx + edge.Rx * Math.Cos(angle), height);
                    if (hit.S > point.S && edge.OnArc(hit)) crossings++;
                }
            }
        }
        return crossings % 2 == 1;
    }
}

internal readonly record struct QaCoverFailure(double Start, double End, double Minimum, double Station);
internal readonly record struct QaCoverSample(double Station, double Cover);

internal static class QaGeometry
{
    internal const double LinearTolerance = 1e-8;
    internal const double StationTolerance = 1e-7;
    internal const double CrossingContactTolerance = 1e-8; // Metres; allow numerical contact with the drawn envelope.
    internal const double TangencyDegrees = 0.000001;
    internal static bool IsLowCover(double cover, double threshold) => cover < threshold - LinearTolerance;
    internal static double PositiveAngle(double angle) => (angle % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);

    internal static string CurveLabel(QaCurveKind kind) => kind switch
    {
        QaCurveKind.Line => "Line",
        QaCurveKind.Ellipse => "Arc",
        QaCurveKind.Parabola => "Parabola"
    };

    internal static IReadOnlyList<double> CoverStations(double start, double end)
    {
        var stations = new List<double> { start };
        double first = Math.Ceiling((start + StationTolerance) / 5) * 5;
        for (double station = first; station < end - StationTolerance; station += 5) stations.Add(station);
        if (end > start + StationTolerance) stations.Add(end);
        else stations[0] = end;
        return stations;
    }

    internal static QaCoverSample MinimumCover(QaSegment curve, QaSegment surface, double halfDiameter, double start, double end)
    {
        var candidates = new List<double> { start, end };
        candidates.AddRange(curve.GradeEquals(surface.Grade((start + end) / 2), start, end));
        return candidates.Select(s => new QaCoverSample(s, surface.At(s) - curve.At(s) - halfDiameter)).OrderBy(p => p.Cover).First();
    }

    internal static IEnumerable<QaInterval> Intrusions(QaSegment curve, QaRegion region, double start, double end)
    {
        var cuts = new List<double> { start, end };
        cuts.AddRange(region.Edges.SelectMany(e => e.Intersections(curve)).Where(s => s > start && s < end));
        var sorted = DistinctStations(cuts);
        for (int i = 1; i < sorted.Count; i++)
        {
            double a = sorted[i - 1], b = sorted[i];
            if (b - a > LinearTolerance && region.Contains(new((a + b) / 2, curve.At((a + b) / 2)))) yield return new(a, b);
        }
    }

    internal static IEnumerable<QaCoverFailure> Cover(QaSegment curve, QaSegment surface, double halfDiameter,
        double threshold, double start, double end)
    {
        // Surface segments are exact straight Civil 3D terrain segments, not resampled terrain.
        double shift = halfDiameter + threshold;
        var boundary = QaEdge.Line(new(surface.Start, surface.At(surface.Start) - shift), new(surface.End, surface.At(surface.End) - shift));
        var cuts = new List<double> { start, end };
        cuts.AddRange(boundary.Intersections(curve).Where(s => s > start && s < end));
        var sorted = DistinctStations(cuts);
        for (int i = 1; i < sorted.Count; i++)
        {
            double a = sorted[i - 1], b = sorted[i], mid = (a + b) / 2;
            if (b - a <= LinearTolerance || !IsLowCover(surface.At(mid) - curve.At(mid) - halfDiameter, threshold)) continue;
            var worst = MinimumCover(curve, surface, halfDiameter, a, b);
            yield return new(a, b, worst.Cover, worst.Station);
        }
    }

    internal static List<QaCoverFailure> MergeCover(IEnumerable<QaCoverFailure> input)
    {
        var merged = new List<QaCoverFailure>();
        foreach (var next in input.OrderBy(x => x.Start).ThenBy(x => x.End))
        {
            if (merged.Count == 0 || next.Start > merged[^1].End + StationTolerance) { merged.Add(next); continue; }
            var prior = merged[^1];
            var worst = prior.Minimum <= next.Minimum ? prior : next;
            merged[^1] = new(prior.Start, Math.Max(prior.End, next.End), worst.Minimum, worst.Station);
        }
        return merged;
    }

    internal static List<QaInterval> Merge(IEnumerable<QaInterval> input)
    {
        var merged = new List<QaInterval>();
        foreach (var next in input.OrderBy(x => x.Start).ThenBy(x => x.End))
        {
            if (merged.Count == 0 || next.Start > merged[^1].End + StationTolerance) merged.Add(next);
            else merged[^1] = new(merged[^1].Start, Math.Max(merged[^1].End, next.End));
        }
        return merged;
    }

    internal static List<double> DistinctStations(IEnumerable<double> values)
    {
        var result = new List<double>();
        foreach (double value in values.Order())
            if (result.Count == 0 || value - result[^1] > StationTolerance) result.Add(value);
        return result;
    }
}

internal static class QaPolynomial
{
    internal static double[] Add(double[] a, double[] b)
    {
        var result = new double[Math.Max(a.Length, b.Length)];
        for (int i = 0; i < result.Length; i++) result[i] = (i < a.Length ? a[i] : 0) + (i < b.Length ? b[i] : 0);
        return result;
    }
    internal static double[] Scale(double[] a, double scale) => a.Select(v => v * scale).ToArray();
    internal static double[] Multiply(double[] a, double[] b)
    {
        var result = new double[a.Length + b.Length - 1];
        for (int i = 0; i < a.Length; i++) for (int j = 0; j < b.Length; j++) result[i + j] += a[i] * b[j];
        return result;
    }
    private static double At(double[] a, double x)
    {
        double value = 0;
        for (int i = a.Length - 1; i >= 0; i--) value = value * x + a[i];
        return value;
    }
    internal static List<double> Roots(double[] coefficients, double start, double end)
    {
        double scale = coefficients.Max(Math.Abs);
        if (scale == 0 || !double.IsFinite(scale)) return new();
        var normalized = Scale(coefficients, 1 / scale).ToList();
        while (normalized.Count > 1 && Math.Abs(normalized[^1]) < 1e-14) normalized.RemoveAt(normalized.Count - 1);
        double[] a = normalized.ToArray();
        if (a.Length == 1) return new();
        if (a.Length == 2)
        {
            double root = -a[0] / a[1];
            return root >= start - 1e-12 && root <= end + 1e-12 ? new() { Math.Clamp(root, start, end) } : new();
        }
        var cuts = new List<double> { start };
        cuts.AddRange(Roots(a.Skip(1).Select((v, i) => v * (i + 1)).ToArray(), start, end));
        cuts.Add(end);
        cuts = cuts.Order().Distinct().ToList();
        var roots = new List<double>();
        foreach (double cut in cuts) if (Math.Abs(At(a, cut)) < 1e-12) roots.Add(cut);
        for (int i = 1; i < cuts.Count; i++)
        {
            double lo = cuts[i - 1], hi = cuts[i], f = At(a, lo), g = At(a, hi);
            if (f * g >= 0) continue;
            for (int j = 0; j < 60; j++)
            {
                double mid = (lo + hi) / 2, value = At(a, mid);
                if (f * value <= 0) hi = mid; else { lo = mid; f = value; }
            }
            roots.Add((lo + hi) / 2);
        }
        return roots.Order().Aggregate(new List<double>(), (list, value) =>
        { if (list.Count == 0 || value - list[^1] > 1e-9) list.Add(value); return list; });
    }
}
