using System;

namespace IntersectUtilities.ArcConstruction;

// Double precision, independent of the AutoCAD runtime so the construction can be tested.
internal readonly record struct ArcVector(double X, double Y, double Z)
{
    public static ArcVector operator +(ArcVector a, ArcVector b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static ArcVector operator -(ArcVector a, ArcVector b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static ArcVector operator *(ArcVector a, double b) => new(a.X * b, a.Y * b, a.Z * b);
    public double Dot(ArcVector b) => X * b.X + Y * b.Y + Z * b.Z;
    public ArcVector Cross(ArcVector b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
    public double Length => Math.Sqrt(Dot(this));
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
}

internal enum TangentArcStatus
{
    InvalidLength, InvalidLine, ParallelLines, NonCoplanarLines, AmbiguousPick, Unrepresentable, Success
}

internal readonly record struct TangentArcSolution(
    ArcVector Center, ArcVector FirstTangent, ArcVector SecondTangent,
    ArcVector Normal, ArcVector StartDirection, double Radius, double Sweep, bool UsesExtensions);

// Expected failures are explicit values. Geometry is consumed only by the Success case.
internal readonly record struct TangentArcResult(TangentArcStatus Status, TangentArcSolution Geometry)
{
    public static TangentArcResult Fault(TangentArcStatus status) => new(status, default);
    public static TangentArcResult Ok(TangentArcSolution geometry) => new(TangentArcStatus.Success, geometry);

#pragma warning disable CS8524 // Unnamed enum values cannot be produced by the solver.
    public T Match<T>(Func<TangentArcSolution, T> success, Func<string, T> fault) => Status switch
    {
        TangentArcStatus.Success => success(Geometry),
        TangentArcStatus.InvalidLength => fault("Arc length must be a finite, positive number."),
        TangentArcStatus.InvalidLine => fault("Select two non-zero-length lines."),
        TangentArcStatus.ParallelLines => fault("Parallel or collinear lines do not define a unique corner arc."),
        TangentArcStatus.NonCoplanarLines => fault("The lines must lie in the same plane; skew lines cannot share a tangent arc."),
        TangentArcStatus.AmbiguousPick => fault("Select each line away from the intersection, on the desired side of the corner."),
        TangentArcStatus.Unrepresentable => fault("The requested arc is too small or too large to construct reliably at these coordinates.")
    };
#pragma warning restore CS8524
}

internal static class TangentArcGeometry
{
    private const double AngularTolerance = 1e-10;
    private const double LinearTolerance = 1e-8;

    // Picks must be points on the selected lines, in the same coordinates as their endpoints.
    // Lines are treated as infinite supports; UsesExtensions reports tangencies outside the segments.
    public static TangentArcResult Solve(
        ArcVector firstStart, ArcVector firstEnd, ArcVector firstPick,
        ArcVector secondStart, ArcVector secondEnd, ArcVector secondPick, double arcLength)
    {
        if (!double.IsFinite(arcLength) || arcLength <= 0)
            return TangentArcResult.Fault(TangentArcStatus.InvalidLength);
        if (!firstStart.IsFinite || !firstEnd.IsFinite || !firstPick.IsFinite ||
            !secondStart.IsFinite || !secondEnd.IsFinite || !secondPick.IsFinite)
            return TangentArcResult.Fault(TangentArcStatus.InvalidLine);

        var first = firstEnd - firstStart;
        var second = secondEnd - secondStart;
        double firstLength = first.Length;
        double secondLength = second.Length;
        if (!double.IsFinite(firstLength) || !double.IsFinite(secondLength) ||
            firstLength <= LinearTolerance || secondLength <= LinearTolerance)
            return TangentArcResult.Fault(TangentArcStatus.InvalidLine);
        first *= 1 / firstLength;
        second *= 1 / secondLength;

        var cross = first.Cross(second);
        double sine = cross.Length;
        if (sine <= AngularTolerance)
            return TangentArcResult.Fault(TangentArcStatus.ParallelLines);
        var planeNormal = cross * (1 / sine);
        var offset = secondStart - firstStart;
        double coordinateScale = Math.Max(Math.Max(firstStart.Length, firstEnd.Length),
            Math.Max(secondStart.Length, secondEnd.Length));
        if (!double.IsFinite(coordinateScale))
            return TangentArcResult.Fault(TangentArcStatus.Unrepresentable);
        double tolerance = Math.Max(LinearTolerance, coordinateScale * 1e-14);
        if (Math.Abs(offset.Dot(planeNormal)) > tolerance)
            return TangentArcResult.Fault(TangentArcStatus.NonCoplanarLines);

        // Intersect the supporting lines without an origin-dependent determinant.
        var intersection = firstStart + first * (offset.Cross(second).Dot(planeNormal) / sine);
        if (!intersection.IsFinite)
            return TangentArcResult.Fault(TangentArcStatus.Unrepresentable);
        double firstSide = (firstPick - intersection).Dot(first);
        double secondSide = (secondPick - intersection).Dot(second);
        if (!double.IsFinite(firstSide) || !double.IsFinite(secondSide))
            return TangentArcResult.Fault(TangentArcStatus.Unrepresentable);
        if (Math.Abs(firstSide) <= tolerance || Math.Abs(secondSide) <= tolerance)
            return TangentArcResult.Fault(TangentArcStatus.AmbiguousPick);
        var firstRay = first * Math.Sign(firstSide);
        var secondRay = second * Math.Sign(secondSide);
        var cornerNormal = firstRay.Cross(secondRay) * (1 / sine);

        // The arc turns through pi minus the angle between the selected rays.
        // atan2 avoids cancellation near a straight corner. Length = radius * sweep (radians).
        double sweep = Math.Atan2(sine, -Math.Clamp(firstRay.Dot(secondRay), -1, 1));
        double radius = arcLength / sweep;
        double setback = radius * Math.Tan(sweep / 2);
        var firstTangent = intersection + firstRay * setback;
        var secondTangent = intersection + secondRay * setback;
        var startDirection = cornerNormal.Cross(firstRay) * -1;
        startDirection *= 1 / startDirection.Length;
        var center = firstTangent - startDirection * radius;
        if (!intersection.IsFinite || !center.IsFinite || !firstTangent.IsFinite || !secondTangent.IsFinite ||
            !double.IsFinite(radius) || !double.IsFinite(setback) || radius <= tolerance ||
            (firstTangent - secondTangent).Length <= tolerance)
            return TangentArcResult.Fault(TangentArcStatus.Unrepresentable);

        // Reject numerically unstable constructions instead of silently drawing a non-tangent arc.
        double checkTolerance = Math.Max(tolerance * 16, radius * 1e-9);
        if (Math.Abs((firstTangent - center).Length - radius) > checkTolerance ||
            Math.Abs((secondTangent - center).Length - radius) > checkTolerance ||
            Math.Abs((secondTangent - center).Dot(secondRay)) > checkTolerance)
            return TangentArcResult.Fault(TangentArcStatus.Unrepresentable);

        double firstParameter = (firstTangent - firstStart).Dot(first);
        double secondParameter = (secondTangent - secondStart).Dot(second);
        bool usesExtensions = firstParameter < -tolerance || firstParameter > firstLength + tolerance ||
            secondParameter < -tolerance || secondParameter > secondLength + tolerance;
        return TangentArcResult.Ok(new(center, firstTangent, secondTangent,
            cornerNormal * -1, startDirection, radius, sweep, usesExtensions));
    }
}
