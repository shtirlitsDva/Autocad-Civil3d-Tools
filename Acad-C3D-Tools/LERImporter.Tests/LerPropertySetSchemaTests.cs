using LERImporter.PropertySets;

namespace LERImporter.Tests;

/// <summary>The schema side: definitions from GML types, and values as AEC's SetAt converts them.</summary>
public class LerPropertySetSchemaTests
{
    [Fact]
    public void Definition_of_a_type_has_its_PsInclude_properties_then_bemaerkning_and_ler_number()
    {
        LerSetDef def = LerSetDef.FromType(typeof(Schema.ProbeledningType));

        Assert.Equal("Probeledning", def.Name);
        Assert.Equal("LERImporter.Schema.ProbeledningType", def.Description);
        Assert.Equal(
            new[]
            {
                ("Ejer", AecDataType.Text),
                ("Dybde", AecDataType.Real),
                ("Antal", AecDataType.Integer),
                ("Farlig", AecDataType.TrueFalse),
                ("Dato", AecDataType.Text),
                ("GmlBemærkning", AecDataType.Text),
                ("LerNummer", AecDataType.Text),
            },
            def.Properties.Select(p => (p.Name, p.Type)));
        Assert.Equal(Option<int>.Of(1), def.IdOf("Dybde"));
        Assert.Equal(Option<int>.Nothing, def.IdOf("Ikke"));
    }

    // xUnit wants public theories and the schema is internal, so the data type travels as
    // its AEC number and a value as the CLR value the case holds.

    [Theory]
    [InlineData((short)0, 0)]
    [InlineData((short)1, 0.0)]
    [InlineData((short)2, "")]
    [InlineData((short)3, false)]
    public void A_missing_value_is_the_type_default(short type, object expected) =>
        Assert.Equal(expected, Raw(AecStreamTests.Ok(AecValue.From((AecDataType)type, null, "P"))));

    [Theory]
    [InlineData((short)2, "pvc", "pvc")]
    [InlineData((short)2, 2.5, "2.5")]
    [InlineData((short)2, true, "True")]
    [InlineData((short)1, 2.5, 2.5)]
    [InlineData((short)1, 3, 3.0)]
    [InlineData((short)1, 1.5f, 1.5)]
    [InlineData((short)0, 42, 42)]
    [InlineData((short)0, (short)7, 7)]
    [InlineData((short)3, false, false)]
    public void A_value_is_converted_as_AEC_converts_it(short type, object value, object expected) =>
        Assert.Equal(expected, Raw(AecStreamTests.Ok(AecValue.From((AecDataType)type, value, "P"))));

    [Theory]
    [InlineData((short)3, "Sand")]
    [InlineData((short)1, "2.5")]
    [InlineData((short)0, 2.5)]
    [InlineData((short)0, "42")]
    public void A_value_AEC_would_refuse_is_a_fault_naming_the_property(short type, object value) =>
        Assert.Contains("Property Dybde", AecStreamTests.Fault(AecValue.From((AecDataType)type, value, "Dybde")));

    private static object Raw(AecValue value) =>
        value.Match<object>(integer: i => i, real: d => d, text: s => s, trueFalse: b => b);
}
