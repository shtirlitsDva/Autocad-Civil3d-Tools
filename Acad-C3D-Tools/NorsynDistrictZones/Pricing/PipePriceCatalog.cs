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
    public string Name { get; set; } = "Default";
    public List<PipePriceEntry> Entries { get; set; } = new();

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
