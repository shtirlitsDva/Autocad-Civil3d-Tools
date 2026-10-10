namespace LERImporter.Schema;

// LerPropertySetSchema.cs names the PsInclude attribute of the GML schema, whose other
// types need a CAD host. The tests compile without them, so the attribute is restated here.
[AttributeUsage(AttributeTargets.Property)]
internal sealed class PsInclude : Attribute
{
}

/// <summary>A GML type in the shape LERImporter's schema classes have.</summary>
internal sealed class ProbeledningType
{
    [PsInclude] public string Ejer { get; set; } = "";
    [PsInclude] public double Dybde { get; set; }
    [PsInclude] public int Antal { get; set; }
    [PsInclude] public bool Farlig { get; set; }
    [PsInclude] public DateTime Dato { get; set; }
    public string IkkeMed { get; set; } = "";
}
