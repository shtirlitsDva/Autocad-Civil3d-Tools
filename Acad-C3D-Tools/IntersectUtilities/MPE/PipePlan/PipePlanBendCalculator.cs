using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlan;

internal enum PipePlanBendStatus
{
    Bend,
    Straight,
    Reversal,
    Degenerate,
    Infeasible,
}

internal readonly record struct PipePlanBendGeometry(
    Point3d Vertex,
    Vector2d IncomingDirection,
    Vector2d OutgoingDirection,
    double TangentLength,
    double Deflection,
    int Sign,
    double Radius,
    Point3d TangentIn,
    Point3d TangentOut);

internal static class PipePlanBendCalculator
{
    public const double DistanceTolerance = 1e-6;
    public const double AngleTolerance = 1e-6;

    public static PipePlanBendStatus TryCompute(
        Point3d previous,
        Point3d vertex,
        Point3d next,
        double radius,
        out PipePlanBendGeometry bend)
    {
        bend = default;

        Vector2d incoming = PipePlanGeometryUtil.To2D(vertex - previous);
        Vector2d outgoing = PipePlanGeometryUtil.To2D(next - vertex);

        double incomingLength = incoming.Length;
        double outgoingLength = outgoing.Length;
        if (incomingLength <= DistanceTolerance || outgoingLength <= DistanceTolerance)
        {
            return PipePlanBendStatus.Degenerate;
        }

        Vector2d incomingUnit = incoming / incomingLength;
        Vector2d outgoingUnit = outgoing / outgoingLength;

        double dot = Math.Clamp(incomingUnit.DotProduct(outgoingUnit), -1.0, 1.0);
        double deflection = Math.Acos(dot);

        if (deflection <= AngleTolerance)
        {
            return PipePlanBendStatus.Straight;
        }

        if (Math.Abs(Math.PI - deflection) <= AngleTolerance)
        {
            return PipePlanBendStatus.Reversal;
        }

        double tangentLength = radius * Math.Tan(deflection / 2.0);
        if (!double.IsFinite(tangentLength))
        {
            return PipePlanBendStatus.Infeasible;
        }

        double cross = (incomingUnit.X * outgoingUnit.Y) - (incomingUnit.Y * outgoingUnit.X);
        int sign = cross >= 0.0 ? 1 : -1;

        Point3d tangentIn = new(
            vertex.X - (incomingUnit.X * tangentLength),
            vertex.Y - (incomingUnit.Y * tangentLength),
            vertex.Z);
        Point3d tangentOut = new(
            vertex.X + (outgoingUnit.X * tangentLength),
            vertex.Y + (outgoingUnit.Y * tangentLength),
            vertex.Z);

        bend = new PipePlanBendGeometry(
            vertex,
            incomingUnit,
            outgoingUnit,
            tangentLength,
            deflection,
            sign,
            radius,
            tangentIn,
            tangentOut);
        return PipePlanBendStatus.Bend;
    }
}
