using LERImporter.PropertySets;

namespace LERImporter.Tests;

/// <summary>
/// The shared codec (AecPropertySetsSHARED) against the streams Civil 3D's AEC wrote:
/// decoding a Civil object and encoding it again gives Civil's stream, token for token;
/// a value it cannot read is marked, not fatal; an edit changes only the value's tokens.
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
        var properties = decoded.Properties
            .Select(p => new AecPropertyDef(p.Name, p.Description, Ok(p.Type)))
            .ToList();
        int formatSlot = (int)golden.Tokens.First(t => t.Kind == AecTokenKind.HardPointer).Integer;

        Assert.Equal(Enumerable.Range(0, properties.Count), decoded.Properties.Select(p => p.Id));
        Assert.Equal<AecToken>(golden.Tokens,
            AecStream.Definition(decoded.Description, properties, LerSetDef.AppliesTo, formatSlot));
    }

    [Fact]
    public void Set_decodes_and_encodes_back_to_Civil_stream()
    {
        GoldenStream golden = GoldenStream.Load("set.txt");

        AecSet decoded = Ok(AecStream.ReadSet(golden.Tokens));
        var values = decoded.Items.Select(StoredValue).ToList();

        Assert.Equal(25, decoded.Items.Count);
        Assert.Equal(Enumerable.Range(0, 25), decoded.Items.Select(i => i.Id));
        Assert.Equal(2, decoded.HeaderPointers.Count);
        Assert.Equal<AecToken>(golden.Tokens,
            AecStream.Set(decoded.HeaderPointers[0], decoded.HeaderPointers[1], values));
    }

    [Fact]
    public void Set_encodes_every_data_type_with_its_variant_and_decodes_it_back()
    {
        var values = new List<AecValue>
        {
            new AecValue.Integer(-7),
            new AecValue.Real(2.5),
            new AecValue.Text("Æblevej 1"),
            new AecValue.TrueFalse(true),
        };

        IReadOnlyList<AecToken> tokens = AecStream.Set(entitySlot: 0, definitionSlot: 1, values);
        AecSet decoded = Ok(AecStream.ReadSet(tokens));

        Assert.Equal(new[] { 0, 1 }, decoded.HeaderPointers);
        Assert.Equal(values, decoded.Items.Select(StoredValue));
    }

    [Fact]
    public void A_value_whose_variant_does_not_fit_its_data_type_is_unreadable_and_the_rest_still_reads()
    {
        List<AecToken> tokens = GoldenStream.Load("set.txt").Tokens.ToList();
        int variant = tokens.FindIndex(t => t == AecToken.Int16(8));
        tokens[variant] = AecToken.Int16(3);

        AecSet decoded = Ok(AecStream.ReadSet(tokens));

        AecItem bad = decoded.Items.Single(i => i.At == variant - 6);
        Assert.Contains("Text property", Unreadable(bad));
        Assert.Equal(24, decoded.Items.Count(i => i.Match(_ => true, _ => true, _ => false)));
    }

    [Fact]
    public void An_empty_variant_is_an_empty_item()
    {
        List<AecToken> tokens = GoldenStream.Load("set.txt").Tokens.ToList();
        int variant = tokens.FindIndex(t => t == AecToken.Int16(8));
        tokens.RemoveAt(variant + 1);
        tokens[variant] = AecToken.Int16(0);

        AecSet decoded = Ok(AecStream.ReadSet(tokens));

        AecItem item = decoded.Items.Single(i => i.At == variant - 6);
        Assert.Equal(AecDataType.Text, item.Match(_ => (AecDataType?)null, e => e.Type, _ => null));
    }

    [Fact]
    public void A_data_type_outside_the_four_manual_types_is_an_unsupported_property()
    {
        GoldenStream golden = GoldenStream.Load("def-Vandledning.txt");
        List<AecToken> tokens = golden.Tokens.ToList();
        // The property files its description, then its name: both "Tværsnitsform".
        int description = tokens.FindIndex(t => t == AecToken.String("Tværsnitsform"));
        int name = tokens.FindIndex(description + 1, t => t == AecToken.String("Tværsnitsform"));
        tokens[name + 1] = AecToken.Int16(4);

        AecDefinition decoded = Ok(AecStream.ReadDefinition(tokens));

        AecDefinedProperty property = decoded.Properties.Single(p => p.Name == "Tværsnitsform");
        Assert.Contains("data type 4", Fault(property.Type));
        Assert.All(decoded.Properties.Where(p => p.Name != "Tværsnitsform"), p => Ok(p.Type));
    }

    [Fact]
    public void An_automatic_property_is_unsupported()
    {
        List<AecToken> tokens = GoldenStream.Load("def-Vandledning.txt").Tokens.ToList();
        int description = tokens.FindIndex(t => t == AecToken.String("Tværsnitsform"));
        int name = tokens.FindIndex(description + 1, t => t == AecToken.String("Tværsnitsform"));
        tokens[name + 2] = AecToken.Bool(true);

        AecDefinition decoded = Ok(AecStream.ReadDefinition(tokens));

        Assert.Contains("automatic", Fault(decoded.Properties.Single(p => p.Name == "Tværsnitsform").Type));
    }

    [Fact]
    public void A_truncated_stream_is_refused() =>
        Assert.Contains("does not end", Fault(AecStream.ReadSet(GoldenStream.Load("set.txt").Tokens.SkipLast(3).ToList())));

    [Fact]
    public void Tokens_after_the_object_are_refused()
    {
        var tokens = GoldenStream.Load("set.txt").Tokens.Append(AecToken.Bool(false)).ToList();

        Assert.Contains("does not end", Fault(AecStream.ReadSet(tokens)));
    }

    [Fact]
    public void A_record_count_that_does_not_match_is_refused()
    {
        List<AecToken> tokens = GoldenStream.Load("set.txt").Tokens.ToList();
        AecSet decoded = Ok(AecStream.ReadSet(tokens));
        tokens[decoded.CountAt] = AecToken.Int32(24);

        Assert.Contains("counts 24", Fault(AecStream.ReadSet(tokens)));
    }

    [Fact]
    public void A_definition_stream_is_not_read_as_a_set() =>
        Fault(AecStream.ReadSet(GoldenStream.Load("def-Elledning.txt").Tokens));

    // ---------------------------------------------------------------- editing

    [Fact]
    public void Editing_a_value_changes_only_that_value()
    {
        IReadOnlyList<AecToken> golden = GoldenStream.Load("set.txt").Tokens;
        AecSet set = Ok(AecStream.ReadSet(golden));
        AecItem.Stored text = set.Items.OfType<AecItem.Stored>().First(i => i.Value.Type == AecDataType.Text);

        IReadOnlyList<AecToken> edited = Ok(AecStream.WithValue(golden, set, text.Id, new AecValue.Text("Ny værdi")));
        AecSet after = Ok(AecStream.ReadSet(edited));

        Assert.Equal(golden.Count, edited.Count);
        Assert.Equal(new[] { text.At + 7 }, Enumerable.Range(0, golden.Count).Where(i => golden[i] != edited[i]));
        Assert.Equal(new AecValue.Text("Ny værdi"), StoredValue(after.ById(text.Id).Match(i => i, () => throw new Xunit.Sdk.XunitException("gone"))));
    }

    [Fact]
    public void Editing_an_empty_value_files_it_and_the_set_reads_back()
    {
        List<AecToken> tokens = GoldenStream.Load("set.txt").Tokens.ToList();
        int variant = tokens.FindIndex(t => t == AecToken.Int16(8));
        tokens.RemoveAt(variant + 1);
        tokens[variant] = AecToken.Int16(0);
        AecSet set = Ok(AecStream.ReadSet(tokens));
        AecItem empty = set.Items.Single(i => i.At == variant - 6);

        IReadOnlyList<AecToken> edited = Ok(AecStream.WithValue(tokens, set, empty.Id, new AecValue.Text("x")));

        Assert.Equal(GoldenStream.Load("set.txt").Tokens.Count, edited.Count);
        Assert.Equal(new AecValue.Text("x"),
            StoredValue(Ok(AecStream.ReadSet(edited)).Items.Single(i => i.Id == empty.Id)));
    }

    [Fact]
    public void Editing_a_property_the_set_stores_no_record_for_adds_one_as_Civil_files_it()
    {
        var values = new List<AecValue> { new AecValue.Integer(1), new AecValue.Text("a") };
        IReadOnlyList<AecToken> tokens = AecStream.Set(0, 1, values);
        AecSet set = Ok(AecStream.ReadSet(tokens));

        IReadOnlyList<AecToken> edited = Ok(AecStream.WithValue(tokens, set, 2, new AecValue.TrueFalse(true)));

        Assert.Equal<AecToken>(AecStream.Set(0, 1, values.Append(new AecValue.TrueFalse(true)).ToList()), edited);
    }

    [Fact]
    public void An_unreadable_value_is_not_edited()
    {
        List<AecToken> tokens = GoldenStream.Load("set.txt").Tokens.ToList();
        int variant = tokens.FindIndex(t => t == AecToken.Int16(8));
        tokens[variant] = AecToken.Int16(3);
        AecSet set = Ok(AecStream.ReadSet(tokens));
        AecItem bad = set.Items.Single(i => i.At == variant - 6);

        Assert.Contains("cannot be edited", Fault(AecStream.WithValue(tokens, set, bad.Id, new AecValue.Text("x"))));
    }

    // ---------------------------------------------------------------- helpers

    private static AecValue StoredValue(AecItem item) => item.Match(
        stored => stored.Value,
        empty => throw new Xunit.Sdk.XunitException($"item {empty.Id} is empty"),
        unreadable => throw new Xunit.Sdk.XunitException($"item {unreadable.Id}: {unreadable.Reason}"));

    private static string Unreadable(AecItem item) => item.Match(
        stored => throw new Xunit.Sdk.XunitException($"item {stored.Id} read as {stored.Value}"),
        empty => throw new Xunit.Sdk.XunitException($"item {empty.Id} read as empty"),
        unreadable => unreadable.Reason);

    internal static T Ok<T>(Result<T> result) =>
        result.Match(value => value, fault => throw new Xunit.Sdk.XunitException("Expected Ok, got Fault: " + fault));

    internal static string Fault<T>(Result<T> result) =>
        result.Match(value => throw new Xunit.Sdk.XunitException($"Expected Fault, got Ok: {value}"), fault => fault);
}
