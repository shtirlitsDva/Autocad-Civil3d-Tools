using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LERImporter.PropertySets;

/// <summary>
/// A property set definition as LERImporter makes it: one per GML type, named after
/// the type ("AfloebsledningType" -> "Afloebsledning").
/// </summary>
/// <remarks>The data types and values are the shared AEC ones (AecPropertySetsSHARED).</remarks>
internal sealed record LerSetDef(string Name, string Description, IReadOnlyList<AecPropertyDef> Properties)
{
    /// <summary>The DXF class names a LER set applies to, in the order the definition files them.</summary>
    public static readonly IReadOnlyList<string> AppliesTo =
        new[] { "AcDbPolyline", "AcDb3dPolyline", "AcDbPoint", "AcDbHatch" };

    public Option<int> IdOf(string propertyName)
    {
        for (int i = 0; i < Properties.Count; i++)
            if (Properties[i].Name == propertyName) return Option<int>.Of(i);
        return Option<int>.Nothing;
    }

    /// <summary>
    /// The definition for a GML type: every [PsInclude] property, then GmlBemærkning and
    /// LerNummer. The same set Civil's CreatePropertySetDefinition builds.
    /// </summary>
    public static LerSetDef FromType(Type type)
    {
        List<AecPropertyDef> properties = type.GetProperties()
            .Where(p => p.CustomAttributes.Any(a => a.AttributeType == typeof(Schema.PsInclude)))
            .Select(p => new AecPropertyDef(p.Name, p.Name, DataTypeOf(p.PropertyType)))
            .ToList();
        properties.Add(new AecPropertyDef(
            "GmlBemærkning", "The bemærkning for graverforespørgsel.", AecDataType.Text));
        properties.Add(new AecPropertyDef("LerNummer", "Ler nummer.", AecDataType.Text));
        return new LerSetDef(type.Name.Replace("Type", ""), type.FullName ?? type.Name, properties);
    }

    private static AecDataType DataTypeOf(Type type) => type.Name switch
    {
        nameof(String) => AecDataType.Text,
        nameof(Boolean) => AecDataType.TrueFalse,
        nameof(Double) => AecDataType.Real,
        nameof(Int32) => AecDataType.Integer,
        _ => AecDataType.Text,
    };
}
