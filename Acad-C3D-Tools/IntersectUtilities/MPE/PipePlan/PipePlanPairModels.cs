using System.Globalization;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

// A bonded (enkelt) pair is drawn as ONE centreline; frem and retur are its parallel
// offsets. See docs/shared-understanding/ppdraw-bonded-pairs.md.

internal enum PipePlanPairRole
{
    Centerline = 0,
    Frem = 1,
    Retur = 2,
}

/// <summary>
/// The pair's lateral geometry: c-c = jacket OD (kOd of the series) + the minimum
/// jacket-to-jacket gap (min x). Millimetres in, metres (drawing units) out.
/// </summary>
internal sealed record PipePlanPairSpacing(PipeSeriesEnum Series, double JacketOdMm, double MinXMm)
{
    public double CentreToCentre => (JacketOdMm + MinXMm) / 1000.0;

    public double Half => CentreToCentre / 2.0;

    public double PipeWidth => JacketOdMm / 1000.0;

    public double CentreToCentreMm => JacketOdMm + MinXMm;

    public bool SameGeometryAs(PipePlanPairSpacing other) =>
        Math.Abs(CentreToCentre - other.CentreToCentre) < 1e-9 &&
        Math.Abs(PipeWidth - other.PipeWidth) < 1e-9;
}

/// <summary>
/// What the drafter authored: the centreline control points, the INNER-pipe bend radius
/// per control point (0 at both ends), and whether frem is flipped to the right of the
/// drawing direction.
/// </summary>
internal sealed record PipePlanPairAuthoring(
    IReadOnlyList<Point3d> ControlPoints,
    IReadOnlyList<double> InnerRadii,
    bool Flip)
{
    public string RadiusDisplay
    {
        get
        {
            List<double> interior = InnerRadii.Skip(1).Take(Math.Max(0, InnerRadii.Count - 2)).Where(r => r > 0.0).ToList();
            if (interior.Count == 0) return "-";
            double first = interior[0];
            return interior.All(r => Math.Abs(r - first) < 1e-6)
                ? first.ToString("0.###", CultureInfo.InvariantCulture)
                : $"{interior.Min().ToString("0.###", CultureInfo.InvariantCulture)}–{interior.Max().ToString("0.###", CultureInfo.InvariantCulture)}";
        }
    }
}

/// <summary>
/// The record every member of a run carries. All three members hold the same authoring
/// data and run token; only <see cref="Role"/> differs. <see cref="Spacing"/> is the
/// spacing the run was last solved with — membership is checked against it.
/// </summary>
internal sealed record PipePlanPairStoredData(
    string RunToken,
    PipePlanPairRole Role,
    PipeSystemEnum System,
    int Dn,
    PipePlanPairSpacing Spacing,
    string StraightSnapToleranceText,
    PipePlanPairAuthoring Authoring)
{
    public string SizeDisplay => $"{System} Enkelt DN{Dn}";

    public PipePlanPairStoredData ForRole(PipePlanPairRole role) => this with { Role = role };
}

/// <summary>What PipePlan is drawing or editing: one polyline, or a bonded pair.</summary>
internal abstract record PipePlanShape
{
    private PipePlanShape() { }

    public static readonly PipePlanShape Single = new SinglePipe();

    public abstract T Match<T>(Func<T> single, Func<BondedPair, T> pair);

    public void Switch(Action single, Action<BondedPair> pair) =>
        Match<Action>(() => single, p => () => pair(p))();

    internal sealed record SinglePipe : PipePlanShape
    {
        public override T Match<T>(Func<T> single, Func<BondedPair, T> pair) => single();
    }

    /// <summary>A bonded pair. FlipLocked is true while continuing an existing run: its
    /// sides are fixed.</summary>
    internal sealed record BondedPair(
        PipeSystemEnum System,
        int Dn,
        PipePlanPairSpacing Spacing,
        bool Flip,
        bool FlipLocked) : PipePlanShape
    {
        public override T Match<T>(Func<T> single, Func<BondedPair, T> pair) => pair(this);

        public string SizeDisplay => $"{System} Enkelt DN{Dn}";

        public string Describe(double innerRadius) =>
            $"{SizeDisplay} · {Spacing.Series} · c-c {Spacing.CentreToCentreMm:0} mm · " +
            $"R {innerRadius.ToString("0.###", CultureInfo.CurrentCulture)} m (inderrør) · " +
            (Flip ? "Frem højre" : "Frem venstre");
    }
}

/// <summary>The three solved vertex lists of a run.</summary>
internal sealed record PipePlanPairSolution(
    PipePlanAnalysis Centre,
    IReadOnlyList<PolylineVertexData> Frem,
    IReadOnlyList<PolylineVertexData> Retur)
{
    public IReadOnlyList<PolylineVertexData> VerticesFor(PipePlanPairRole role) => role switch
    {
        PipePlanPairRole.Centerline => Centre.Vertices,
        PipePlanPairRole.Frem => Frem,
        PipePlanPairRole.Retur => Retur,
    };
}
