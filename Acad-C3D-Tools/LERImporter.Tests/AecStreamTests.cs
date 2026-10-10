using LERImporter.Host.Brx;
using LERImporter.PropertySets;

namespace LERImporter.Tests;

/// <summary>
/// The codec against the streams Civil 3D's AEC wrote: decoding a Civil object and
/// encoding it again gives Civil's stream, token for token.
/// </summary>
public class AecStreamTests
{
    public static TheoryData<string> DefinitionFiles()
    {
        var data = new TheoryData<string>();
        foreach (string file in GoldenStream.Definitions()) data.Add(file);
        return data;
    }

    [Fact]
    public void There_is_a_golden_definition_for_every_LER_type() =>
        Assert.Equal(12, GoldenStream.Definitions().Count());

    [Fact]
    public void Standard_format_encodes_as_Civil_files_it() =>
        Assert.Equal<AecToken>(GoldenStream.Load("format.txt").Tokens, AecStream.FormatStandard());

    [Theory]
    [MemberData(nameof(DefinitionFiles))]
    public void Definition_decodes_and_encodes_back_to_Civil_stream(string file)
    {
        GoldenStream golden = GoldenStream.Load(file);

        AecDefinition decoded = Ok(AecStream.ReadDefinition(golden.Tokens));
        var def = new LerSetDef(file["def-".Length..^".txt".Length], decoded.Description, decoded.Properties);

        Assert.Equal<AecToken>(golden.Tokens, AecStream.Definition(def, decoded.FormatSlot));
    }

    [Fact]
    public void Set_decodes_and_encodes_back_to_Civil_stream()
    {
        GoldenStream golden = GoldenStream.Load("set.txt");

        AecSet decoded = Ok(AecStream.ReadSet(golden.Tokens));
        var values = decoded.Values.Select(v => (TypeOf(v.Value), v.Value)).ToList();

        Assert.Equal(25, decoded.Values.Count);
        Assert.Equal(Enumerable.Range(0, 25), decoded.Values.Select(v => v.Id));
        Assert.Equal<AecToken>(golden.Tokens, AecStream.Set(decoded.EntitySlot, decoded.DefinitionSlot, values));
    }

    [Fact]
    public void Set_encodes_every_data_type_with_its_variant_and_decodes_it_back()
    {
        var values = new List<(LerDataType, LerValue)>
        {
            (LerDataType.Integer, new LerValue.Integer(-7)),
            (LerDataType.Real, new LerValue.Real(2.5)),
            (LerDataType.Text, new LerValue.Text("Æblevej 1")),
            (LerDataType.TrueFalse, new LerValue.TrueFalse(true)),
        };

        IReadOnlyList<AecToken> tokens = AecStream.Set(entitySlot: 0, definitionSlot: 1, values);
        AecSet decoded = Ok(AecStream.ReadSet(tokens));

        Assert.Equal((0, 1), (decoded.EntitySlot, decoded.DefinitionSlot));
        Assert.Equal(values.Select(v => v.Item2), decoded.Values.Select(v => v.Value));
    }

    [Fact]
    public void A_value_whose_variant_does_not_fit_its_data_type_is_refused()
    {
        List<AecToken> tokens = GoldenStream.Load("set.txt").Tokens.ToList();
        int variant = tokens.FindIndex(t => t == AecToken.Int16(8));
        tokens[variant] = AecToken.Int16(3);

        string fault = Fault(AecStream.ReadSet(tokens));

        Assert.Contains($"token {variant}", fault);
    }

    [Fact]
    public void A_data_type_outside_the_four_manual_types_is_refused()
    {
        GoldenStream golden = GoldenStream.Load("def-Vandledning.txt");
        List<AecToken> tokens = golden.Tokens.ToList();
        // The property files its description, then its name: both "Tværsnitsform".
        int description = tokens.FindIndex(t => t == AecToken.String("Tværsnitsform"));
        int name = tokens.FindIndex(description + 1, t => t == AecToken.String("Tværsnitsform"));
        int dataType = name + 1;
        tokens[dataType] = AecToken.Int16(4);

        Assert.Contains("data type 4", Fault(AecStream.ReadDefinition(tokens)));
    }

    [Fact]
    public void A_truncated_stream_is_refused() =>
        Assert.Contains("stream ends", Fault(AecStream.ReadSet(GoldenStream.Load("set.txt").Tokens.SkipLast(3).ToList())));

    [Fact]
    public void Tokens_after_the_object_are_refused()
    {
        var tokens = GoldenStream.Load("set.txt").Tokens.Append(AecToken.Bool(false)).ToList();

        Assert.Contains("tokens left", Fault(AecStream.ReadSet(tokens)));
    }

    [Fact]
    public void A_definition_stream_is_not_read_as_a_set() =>
        Fault(AecStream.ReadSet(GoldenStream.Load("def-Elledning.txt").Tokens));

    private static LerDataType TypeOf(LerValue value) => value.Match(
        integer: _ => LerDataType.Integer,
        real: _ => LerDataType.Real,
        text: _ => LerDataType.Text,
        trueFalse: _ => LerDataType.TrueFalse);

    internal static T Ok<T>(Result<T> result) =>
        result.Match(value => value, fault => throw new Xunit.Sdk.XunitException("Expected Ok, got Fault: " + fault));

    internal static string Fault<T>(Result<T> result) =>
        result.Match(value => throw new Xunit.Sdk.XunitException($"Expected Fault, got Ok: {value}"), fault => fault);
}
