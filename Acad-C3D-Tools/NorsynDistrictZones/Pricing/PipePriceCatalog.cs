using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

namespace NorsynDistrictZones.Pricing;

/// <summary>One editable price row: per-metre pipe price + per-fitting (stik) price for a (type, DN).</summary>
public sealed class PipePriceEntry
{
    public PipeType PipeType { get; set; }
    public int Dn { get; set; }
    public double PricePerMeter { get; set; }
    public double PricePerFitting { get; set; }
}

/// <summary>
/// A named, per-project price configuration (prices only). Seeded as a COPY of the
/// NorsynHydraulicShared defaults — edits never write back to the library. Persisted
/// per drawing via FlexDataStore; the user can copy / rename / import / export / switch.
/// </summary>
public sealed class PipePriceCatalog
{
    /// <summary>The reserved name of the built-in catalog seeded from the NHS defaults.</summary>
    public const string DefaultName = "Default";

    public string Name { get; set; } = DefaultName;
    public List<PipePriceEntry> Entries { get; set; } = new();

    /// <summary>
    /// The built-in "Default" catalog is a read-only copy of the NorsynHydraulicShared
    /// prices — it cannot be edited, renamed or deleted. Only user-created catalogs are
    /// editable. Derived from the name (the same anchor <see cref="CatalogStore"/> uses),
    /// so it survives a JSON round-trip without storing a separate flag.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsReadOnly => string.Equals(Name, DefaultName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Builds a fresh catalog from the NHS embedded defaults (a copy, never a live reference).</summary>
    public static PipePriceCatalog SeedFromDefaults(string name = "Default")
    {
        var catalog = new PipePriceCatalog { Name = name };
        var pipeTypes = new PipeTypes(new DefaultHydraulicSettings());

        foreach (PipeType pt in Enum.GetValues<PipeType>())
        {
            if (pipeTypes.GetPipeType(pt) is not PipeBase pb) continue;
            foreach (Dim dim in pb.GetAllDimsSorted())
            {
                catalog.Entries.Add(new PipePriceEntry
                {
                    PipeType = pt,
                    Dn = dim.NominalDiameter,
                    PricePerMeter = dim.Price_m,
                    PricePerFitting = dim.Price_stk,
                });
            }
        }
        return catalog;
    }

    /// <summary>Deep copy under a new name (for the editor's Copy command).</summary>
    public PipePriceCatalog Copy(string newName) => new()
    {
        Name = newName,
        Entries = Entries.Select(e => new PipePriceEntry
        {
            PipeType = e.PipeType,
            Dn = e.Dn,
            PricePerMeter = e.PricePerMeter,
            PricePerFitting = e.PricePerFitting,
        }).ToList(),
    };

    public PipePriceEntry? Find(PipeType pipeType, int dn) =>
        Entries.FirstOrDefault(e => e.PipeType == pipeType && e.Dn == dn);
}
