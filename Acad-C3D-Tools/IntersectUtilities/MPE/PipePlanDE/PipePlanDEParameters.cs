namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// One row of the German "Regel-Grabenprofil" table, in metres. The trench is modelled
/// as symmetric, so the four edge insets collapse to two inputs: <c>z1/z3</c> (left,
/// applied to both z1 and z3) and <c>z2/z4</c> (right, applied to both z2 and z4).
/// The six stored inputs are, in order: z1/z3, d, x, z2/z4, B, B1. The two band widths
/// b and b1 are NOT stored — they are pure sums of the inputs (see <see cref="ComputeBandWidth"/>),
/// shown read-only in the UI. Pipe drawing consumes only <see cref="D"/> and
/// <see cref="X"/>; PDTRENCH consumes <see cref="B"/>.
/// </summary>
internal sealed class PipePlanDEParameters
{
    /// <summary>The six editable inputs, in storage order.</summary>
    public static readonly IReadOnlyList<string> InputLabels =
        ["z1/z3", "d", "x", "z2/z4", "B", "B1"];

    /// <summary>All eight table columns in display order: the six inputs with the two
    /// computed band widths (b, b1) inserted after the pipe-spacing inputs.</summary>
    public static readonly IReadOnlyList<string> DisplayLabels =
        ["z1/z3", "d", "x", "z2/z4", "b", "b1", "B", "B1"];

    public const int InputCount = 6;

    private const int IndexZ13 = 0;
    private const int IndexD = 1;
    private const int IndexX = 2;
    private const int IndexZ24 = 3;
    private const int IndexB = 4;
    private const int IndexB1 = 5;

    private readonly double[] _inputs;

    public PipePlanDEParameters(IReadOnlyList<double> inputs)
    {
        if (inputs.Count != InputCount)
        {
            throw new ArgumentException($"Expected {InputCount} values, got {inputs.Count}.", nameof(inputs));
        }

        _inputs = inputs.ToArray();
    }

    public IReadOnlyList<double> Inputs => _inputs;

    public double this[int index] => _inputs[index];

    /// <summary>Symmetric left inset (z1 = z3).</summary>
    public double Z1Z3 => _inputs[IndexZ13];

    /// <summary>Außerer Durchmesser der Mantelrohre — the drawn pipe band width.</summary>
    public double D => _inputs[IndexD];

    /// <summary>Clear gap between the two pipes.</summary>
    public double X => _inputs[IndexX];

    /// <summary>Symmetric right inset (z2 = z4).</summary>
    public double Z2Z4 => _inputs[IndexZ24];

    /// <summary>Regelgrabenbreite — the total trench width consumed by PDTRENCH.</summary>
    public double B => _inputs[IndexB];

    /// <summary>Lower trench width.</summary>
    public double B1 => _inputs[IndexB1];

    /// <summary>Centre-to-centre spacing of supply and return = one mantle OD + the gap.</summary>
    public double PipeSpacing => D + X;

    /// <summary>
    /// The trench band width b = z1 + 2·d + x + z2. Under the symmetric model
    /// (z1 = z3 = z1/z3, z2 = z4 = z2/z4) b1 equals b, so both display columns use this.
    /// Static so the model and the live table recompute share one definition.
    /// </summary>
    public static double ComputeBandWidth(double z1z3, double d, double x, double z2z4)
        => z1z3 + (2.0 * d) + x + z2z4;

    /// <summary>
    /// Validates that every input is a finite, non-negative metre figure and that the
    /// drawing-driving inputs are positive: d (pipe width / spacing) and B (trench
    /// width). Used before persisting an edit and when reading a stored override so
    /// invalid geometry never reaches PDDRAW/PDTRENCH.
    /// </summary>
    public bool TryValidate(out string error)
    {
        for (int i = 0; i < InputCount; i++)
        {
            if (!double.IsFinite(_inputs[i]))
            {
                error = $"{InputLabels[i]} er ikke et gyldigt tal.";
                return false;
            }

            if (_inputs[i] < 0.0)
            {
                error = $"{InputLabels[i]} skal være ≥ 0.";
                return false;
            }
        }

        if (_inputs[IndexD] <= 0.0)
        {
            error = "d skal være > 0.";
            return false;
        }

        if (_inputs[IndexB] <= 0.0)
        {
            error = "B skal være > 0.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
