namespace AutoProfileSolver.Alignment;

/// <summary>Knobs for the alignment solver (port of solver.py AlignmentSettings).</summary>
public sealed class AlignmentSettings
{
    public double Ds = 0.5;
    public double ClearanceM = 0.05;
    public double StraightCurvRM = 8000.0;     // |local R| above this ⇒ straight run
    public int MinGradePts = 4;
    public int SlopeRefineIters = 2;
    public double LpRminSafety = 1.15;          // LP targets R ≥ this·R_min for headroom
    public double FitSafety = 1.0;              // emitted arc must have R ≥ this·R_min
    public double MergeDeflectionDeg = 0.5;
    public double MergeDevTolM = 0.03;
    public int MaxRepairIters = 10;
    public double RepairMarginM = 0.01;
    public double SpanDevTolM = 0.05;           // curved span must track the LP within this
    public double SpanTanTolDeg = 0.2;          // accept a single arc if its end tangent is within this
    public int MaxSubdivDepth = 7;
    public double CoverAllowanceM = 0.02;       // max rise above cover (accept + over-route budget)
}
