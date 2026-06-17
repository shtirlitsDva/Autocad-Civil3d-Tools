namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// The ten geometry values of one row of the German "Regel-Grabenprofil" table,
/// in fixed column order (z1, z3, d, x, z2, z4, b, b1, B, B1), all in metres.
/// Stored as an ordered array so the column labels map 1:1 to the editable UI
/// cells and we avoid the b/B casing clash. Pipe drawing only consumes <see cref="D"/>
/// (mantle outer diameter) and <see cref="X"/> (clear gap between the two pipes);
/// the remaining trench widths are carried for the later PDTRENCH command.
/// </summary>
internal sealed class PipePlanDEParameters
{
    public static readonly IReadOnlyList<string> ColumnLabels =
        ["z1", "z3", "d", "x", "z2", "z4", "b", "b1", "B", "B1"];

    public const int ColumnCount = 10;
    private const int IndexD = 2;
    private const int IndexX = 3;
    private const int IndexB = 8;

    private readonly double[] _values;

    public PipePlanDEParameters(IReadOnlyList<double> values)
    {
        if (values.Count != ColumnCount)
        {
            throw new ArgumentException($"Expected {ColumnCount} values, got {values.Count}.", nameof(values));
        }

        _values = values.ToArray();
    }

    public IReadOnlyList<double> Values => _values;

    public double this[int index] => _values[index];

    /// <summary>Außerer Durchmesser der Mantelrohre — the drawn pipe band width.</summary>
    public double D => _values[IndexD];

    /// <summary>Clear gap between the two pipes.</summary>
    public double X => _values[IndexX];

    /// <summary>Centre-to-centre spacing of supply and return = one mantle OD + the gap.</summary>
    public double PipeSpacing => D + X;

    /// <summary>
    /// Validates that every value is a finite, non-negative metre figure and that the
    /// drawing-driving columns are positive: d (pipe width / spacing) and B (trench
    /// buffer width). Used before persisting an edit and when reading a stored override
    /// so invalid geometry never reaches PDDRAW/PDTRENCH.
    /// </summary>
    public bool TryValidate(out string error)
    {
        for (int i = 0; i < ColumnCount; i++)
        {
            if (!double.IsFinite(_values[i]))
            {
                error = $"{ColumnLabels[i]} er ikke et gyldigt tal.";
                return false;
            }

            if (_values[i] < 0.0)
            {
                error = $"{ColumnLabels[i]} skal være ≥ 0.";
                return false;
            }
        }

        if (_values[IndexD] <= 0.0)
        {
            error = "d skal være > 0.";
            return false;
        }

        if (_values[IndexB] <= 0.0)
        {
            error = "B skal være > 0.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
