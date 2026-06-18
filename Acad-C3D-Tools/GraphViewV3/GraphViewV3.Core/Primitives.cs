namespace GraphViewV3.Core;

/// <summary>A 2D point in drawing (plan) coordinates. The live graph is schematic,
/// so we project to XY and ignore Z.</summary>
public readonly record struct Pt(double X, double Y)
{
    public double DistanceTo(Pt o)
    {
        double dx = X - o.X, dy = Y - o.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
