namespace IntersectUtilities.MPE.PipePlanDE;

internal sealed record PipePlanDEStandardRow(int Dn, string Label, PipePlanDEParameters Parameters);

/// <summary>
/// Built-in defaults transcribed from the Regel-Grabenprofil table (values in
/// metres). Keyed by nominal DN. The 32/40 band is keyed under 32. These are the
/// fallback values used whenever a drawing has no per-DN override in
/// <see cref="PipePlanDEParameterStore"/>.
/// </summary>
internal static class PipePlanDEStandardTable
{
    // Inputs: z1/z3, d, x, z2/z4, B, B1 (b and b1 are computed sums, not stored).
    private static readonly PipePlanDEStandardRow[] _rows =
    [
        Row(25,  "25/90",     0.20, 0.09, 0.20, 0.20, 0.90, 1.10),
        Row(32,  "32/110",    0.20, 0.11, 0.20, 0.20, 0.95, 1.15),
        Row(40,  "40/110",    0.20, 0.11, 0.20, 0.20, 0.95, 1.15),
        Row(50,  "50/125",    0.20, 0.13, 0.20, 0.20, 0.95, 1.15),
        Row(65,  "65/140",    0.20, 0.14, 0.20, 0.20, 1.00, 1.20),
        Row(80,  "80/160",    0.20, 0.16, 0.20, 0.20, 1.05, 1.25),
        Row(100, "100/200",   0.20, 0.20, 0.20, 0.20, 1.10, 1.30),
        Row(125, "125/225",   0.20, 0.23, 0.20, 0.20, 1.35, 1.50),
        Row(150, "150/250",   0.20, 0.25, 0.20, 0.20, 1.40, 1.55),
        Row(200, "200/315",   0.20, 0.32, 0.20, 0.20, 1.55, 1.70),
        Row(250, "250/400",   0.20, 0.40, 0.20, 0.20, 1.70, 1.85),
        Row(300, "300/450",   0.40, 0.45, 0.35, 0.40, 2.15, 2.25),
        Row(350, "350/500",   0.40, 0.50, 0.35, 0.40, 2.25, 2.35),
        Row(400, "400/560",   0.40, 0.56, 0.35, 0.40, 2.40, 2.50),
        Row(450, "450/630",   0.40, 0.63, 0.35, 0.40, 2.50, 2.60),
        Row(500, "500/670",   0.40, 0.67, 0.35, 0.40, 2.60, 2.70),
        Row(600, "600/800",   0.40, 0.75, 0.35, 0.40, 2.85, 2.95),
    ];

    // Selectable nominal sizes for drawing. DN 32 and DN 40 share the 110 casing
    // and the same default values, but each is its own independently editable row.
    public static readonly IReadOnlyList<int> SelectableDns =
        [25, 32, 40, 50, 65, 80, 100, 125, 150, 200, 250, 300, 350, 400, 450, 500, 600];

    private static PipePlanDEStandardRow Row(int dn, string label, params double[] values)
        => new(dn, label, new PipePlanDEParameters(values));

    public static IReadOnlyList<PipePlanDEStandardRow> Rows => _rows;

    public static bool TryGet(int dn, out PipePlanDEStandardRow row)
    {
        foreach (PipePlanDEStandardRow candidate in _rows)
        {
            if (candidate.Dn == dn)
            {
                row = candidate;
                return true;
            }
        }

        row = null!;
        return false;
    }

    public static PipePlanDEParameters? DefaultFor(int dn)
        => TryGet(dn, out PipePlanDEStandardRow row) ? row.Parameters : null;
}
