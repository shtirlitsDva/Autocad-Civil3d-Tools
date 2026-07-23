namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Minimum elastic bending radius (metres) of the steel medium pipe, keyed by nominal
/// DN. Only the steel pipe bends elastically, so this is the physical floor on how
/// tight a corner can be drawn. PDDRAW applies it to the INNER pipe of a bend; the
/// centreline and outer pipe radii are derived from it by the ± half-spacing offset
/// (see <see cref="PipePlanDEGeometryBuilder"/>).
///
/// Transcribed from the manufacturer's "Min. elastisk bukkeradius" table, which is
/// indexed by steel outer diameter (26.9 mm = DN20 … 508 mm = DN500). The DN20 row
/// (13 m) is omitted because DN20 is not a PDDRAW drawing size; DN600 has no published
/// value and uses 305 m. Every DN in <see cref="PipePlanDEStandardTable.SelectableDns"/>
/// is covered, so a selectable size never lacks a radius.
/// </summary>
internal static class PipePlanDEBendRadiusTable
{
    private static readonly IReadOnlyDictionary<int, double> _radii = new Dictionary<int, double>
    {
        [25] = 17.0,
        [32] = 21.0,
        [40] = 24.0,
        [50] = 30.0,
        [65] = 38.0,
        [80] = 44.0,
        [100] = 57.0,
        [125] = 70.0,
        [150] = 84.0,
        [200] = 110.0,
        [250] = 137.0,
        [300] = 162.0,
        [350] = 178.0,
        [400] = 203.0,
        [450] = 229.0,
        [500] = 254.0,
        [600] = 305.0,
    };

    /// <summary>Minimum elastic bending radius in metres for the steel medium pipe of
    /// the given nominal DN.</summary>
    public static bool TryGet(int dn, out double rMinMetres) => _radii.TryGetValue(dn, out rMinMetres);

    /// <summary>
    /// Builds the default per-vertex inner-pipe bending-radius array for a run of
    /// <paramref name="count"/> control points: 0 at both endpoints (endpoints never
    /// bend), the DN's table R_min at every interior corner. Returns false when the DN
    /// has no published radius.
    /// </summary>
    public static bool TryDefaultRadii(int dn, int count, out double[] radii, out string error)
    {
        radii = [];
        error = string.Empty;
        if (!TryGet(dn, out double rMin))
        {
            error = $"Ingen bukkeradius for DN {dn}.";
            return false;
        }

        radii = new double[Math.Max(0, count)];
        for (int i = 0; i < radii.Length; i++)
        {
            radii[i] = (i == 0 || i == radii.Length - 1) ? 0.0 : rMin;
        }

        return true;
    }

    /// <summary>
    /// Enforces the physical floor: an inner-pipe bending radius may not be smaller than
    /// the DN's minimum elastic bending radius. Returns false (with a Danish message)
    /// when <paramref name="value"/> is below R_min or non-positive.
    /// </summary>
    public static bool Validate(int dn, double value, out string error)
    {
        error = string.Empty;
        if (!TryGet(dn, out double rMin))
        {
            error = $"Ingen bukkeradius for DN {dn}.";
            return false;
        }

        if (!(value > 0.0))
        {
            error = "Radius skal være > 0.";
            return false;
        }

        // Small tolerance so a radius typed exactly as the table value is accepted.
        if (value < rMin - 1e-6)
        {
            error = $"Radius {value:0.###} m er under min. elastisk bukkeradius for DN {dn} ({rMin:0.###} m).";
            return false;
        }

        return true;
    }
}
