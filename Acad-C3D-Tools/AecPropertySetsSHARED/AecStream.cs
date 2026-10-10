using System;
using System.Collections.Generic;
using System.Linq;

// AecDataType / AecTokenKind switches name every member and have no discard arm.
#pragma warning disable CS8524

namespace Norsyn.AecPropertySets;

// The DWG filer stream of AEC's property-set objects, as BricsCAD's AEC classes (ODA's
// AecScheduleData) file and read it. Pure: no host types, so it is tested without CAD.
//
// MEASURED on BricsCAD V26.2 (2026-10-10), recording a file filer over sets and definitions
// that Civil 3D 2025 wrote (Svogerslev LER, 1,981 sets, 40,075 values). A set and a
// definition written from this layout, saved by BricsCAD, read back in Civil 3D's AEC API
// value for value.
//
// The stream starts after the AcDbObject part (owner and reactors, which the host writes):
//   AecDbObject     Bool true, Bool false, I16 80
//   AecDbDictRecord Bool true, Bool true, I16 tag, I16 80, Str description, I16 80, Str "",
//                   Bool false, Byte 0
//   then the class's own fields:
//   1003 format     the "Standard" format, a constant (FormatStandard)
//   1002 definition I16 80, I32 n, I16 80, Str "", I16 80, I32 n, n x 1007 property, AppliesTo
//                   (I16 k, k x Str class), I16 3, Bool false, Bool false, Byte 0, then End
//   1007 property   Bool true, Bool true, I16 1007, I16 80, Str description, I16 80, I32 id,
//                   Str name, I16 data type, Bool automatic, HPtr format, Value(default),
//                   Bool false, I16 80, Str "", I16 80, I32 0, I16 0, Str "", Byte 2, I32 0
//   1001 set        I16 80, HPtr entity, HPtr definition, I16 80, Str "", I16 80, I32 n,
//                   n x 1006 item, then End
//   1006 item       Bool true, Bool true, I16 1006, I16 80, Str "", I16 80, I32 id,
//                   I16 data type, Value, Bool false, I16 80, Str "", I16 80, I32 0, I16 0
//   Value           I16 VARIANT type, then the value: 2 I16 / 3 I32 (Integer), 5 Double (Real),
//                   8 Str (Text), 11 Bool (TrueFalse); 0 / 1 (empty) carry no value
//   End             Bool false, I16 80, I16 80, I16 0
// 80 is AEC's filing version.
//
// ENCODING writes exactly that layout (what Civil files for a manual property).
// DECODING is the layout NorsynDrawingTools' native reader proved on LER and FJV drawings
// (Core DistrictHeating/Ler/PropertySetDecoder.cpp): it finds the records by their opening
// (Bool, Bool, I16 tag, I16 80), reads the fields it needs at their place in the record, and
// tolerates the rest, so sets that other programs wrote decode too. A structural surprise
// (marker, tag, record count) refuses the object; a property or value it cannot read is
// marked so and the rest of the object still reads.
// EDITING patches the recorded stream (WithValue): only the value's tokens change, so every
// field this code does not understand survives as it was filed.

internal enum AecTokenKind
{
    Bool,
    Byte,
    Int16,
    Int32,
    Double,
    String,
    SoftPointer,
    HardPointer,
}

/// <summary>
/// One filer call. A pointer carries a slot: an index into the id table the host keeps
/// (encoding: the ids it passes in; decoding: the ids it recorded).
/// </summary>
internal readonly record struct AecToken(AecTokenKind Kind, long Integer, double Real, string Text)
{
    public static AecToken Bool(bool value) => new(AecTokenKind.Bool, value ? 1 : 0, 0, "");
    public static AecToken Byte(byte value) => new(AecTokenKind.Byte, value, 0, "");
    public static AecToken Int16(short value) => new(AecTokenKind.Int16, value, 0, "");
    public static AecToken Int32(int value) => new(AecTokenKind.Int32, value, 0, "");
    public static AecToken Double(double value) => new(AecTokenKind.Double, 0, value, "");
    public static AecToken String(string value) => new(AecTokenKind.String, 0, 0, value);
    public static AecToken Soft(int slot) => new(AecTokenKind.SoftPointer, slot, 0, "");
    public static AecToken Hard(int slot) => new(AecTokenKind.HardPointer, slot, 0, "");

    public bool IsPointer => Kind is AecTokenKind.SoftPointer or AecTokenKind.HardPointer;

    public override string ToString() => Kind switch
    {
        AecTokenKind.Bool => "Bool " + (Integer != 0),
        AecTokenKind.Byte => "Byte " + Integer,
        AecTokenKind.Int16 => "I16 " + Integer,
        AecTokenKind.Int32 => "I32 " + Integer,
        AecTokenKind.Double => "Dbl " + Real.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        AecTokenKind.String => "Str " + Text,
        AecTokenKind.SoftPointer => "SPtr #" + Integer,
        AecTokenKind.HardPointer => "HPtr #" + Integer,
    };
}

/// <summary>
/// A property of a decoded definition. <see cref="Type"/> is a fault when the property is
/// not a manual Integer / Real / Text / True-False one (automatic, another data type): it is
/// shown, never edited.
/// </summary>
internal sealed record AecDefinedProperty(int Id, string Name, string Description, Result<AecDataType> Type);

/// <summary>A decoded definition: its description and its properties in filed order.</summary>
internal sealed record AecDefinition(string Description, IReadOnlyList<AecDefinedProperty> Properties)
{
    public Option<AecDefinedProperty> ById(int id)
    {
        foreach (AecDefinedProperty p in Properties)
            if (p.Id == id) return Option<AecDefinedProperty>.Of(p);
        return Option<AecDefinedProperty>.Nothing;
    }

    public Option<AecDefinedProperty> ByName(string name)
    {
        foreach (AecDefinedProperty p in Properties)
            if (p.Name == name) return Option<AecDefinedProperty>.Of(p);
        return Option<AecDefinedProperty>.Nothing;
    }
}

/// <summary>One 1006 record of a set: what it stores for one property. At is its tag's token.</summary>
internal abstract record AecItem(int Id, int At)
{
    public abstract TOut Match<TOut>(
        Func<Stored, TOut> stored, Func<Empty, TOut> empty, Func<Unreadable, TOut> unreadable);

    /// <summary>A value of a manual data type.</summary>
    internal sealed record Stored(int Id, int At, AecValue Value) : AecItem(Id, At)
    {
        public override TOut Match<TOut>(
            Func<Stored, TOut> stored, Func<Empty, TOut> empty, Func<Unreadable, TOut> unreadable) => stored(this);
    }

    /// <summary>An empty VARIANT: the record is there, its value is not.</summary>
    internal sealed record Empty(int Id, int At, AecDataType Type) : AecItem(Id, At)
    {
        public override TOut Match<TOut>(
            Func<Stored, TOut> stored, Func<Empty, TOut> empty, Func<Unreadable, TOut> unreadable) => empty(this);
    }

    /// <summary>A record whose data type or value this code does not read.</summary>
    internal sealed record Unreadable(int Id, int At, string Reason) : AecItem(Id, At)
    {
        public override TOut Match<TOut>(
            Func<Stored, TOut> stored, Func<Empty, TOut> empty, Func<Unreadable, TOut> unreadable) => unreadable(this);
    }
}

/// <summary>
/// A decoded set: the hard pointers its header files (the entity and the definition; the
/// host tells them apart by class), its records, and where its record count is filed.
/// </summary>
internal sealed record AecSet(IReadOnlyList<int> HeaderPointers, IReadOnlyList<AecItem> Items, int CountAt)
{
    public Option<AecItem> ById(int id)
    {
        foreach (AecItem item in Items)
            if (item.Id == id) return Option<AecItem>.Of(item);
        return Option<AecItem>.Nothing;
    }
}

internal static class AecStream
{
    private const short Version = 80;
    private const short TagSet = 1001;
    private const short TagDefinition = 1002;
    private const short TagFormat = 1003;
    private const short TagItem = 1006;
    private const short TagProperty = 1007;

    // From the header record's tag to its I32 record count.
    private const int DefinitionCountOffset = 12;
    private const int SetCountOffset = 13;

    // ---------------------------------------------------------------- encoding

    /// <summary>The "Standard" format of a Civil 3D LER drawing, as Civil files it.</summary>
    public static IReadOnlyList<AecToken> FormatStandard()
    {
        var t = Head(TagFormat, "");
        t.AddRange(new[]
        {
            AecToken.Int16(Version), AecToken.String(""), AecToken.String(""),
            AecToken.Int16(0), AecToken.Int16(2), AecToken.Int16(2), AecToken.Int16(0), AecToken.Int16(2),
            AecToken.Bool(true), AecToken.Bool(true), AecToken.Bool(false), AecToken.Bool(false),
            AecToken.Double(0), AecToken.Int16(0),
            AecToken.String("?"), AecToken.String("False"), AecToken.String("True"),
            AecToken.Int16(3), AecToken.Int16(0), AecToken.Double(1), AecToken.String("NA"),
            AecToken.Int16(Version), AecToken.String(""), AecToken.Int16(Version), AecToken.Int32(0),
            AecToken.Int16(0), AecToken.String(""), AecToken.Int32(0),
        });
        t.AddRange(End());
        return t;
    }

    /// <param name="properties">The properties; each one's id is its index.</param>
    /// <param name="appliesTo">The DXF class names the definition applies to.</param>
    /// <param name="formatSlot">The slot of the format every property points at.</param>
    public static IReadOnlyList<AecToken> Definition(
        string description, IReadOnlyList<AecPropertyDef> properties, IReadOnlyList<string> appliesTo, int formatSlot)
    {
        int n = properties.Count;
        var t = Head(TagDefinition, description);
        t.AddRange(new[]
        {
            AecToken.Int16(Version), AecToken.Int32(n),
            AecToken.Int16(Version), AecToken.String(""), AecToken.Int16(Version), AecToken.Int32(n),
        });
        for (int id = 0; id < n; id++)
        {
            AecPropertyDef p = properties[id];
            t.AddRange(new[]
            {
                AecToken.Bool(true), AecToken.Bool(true), AecToken.Int16(TagProperty),
                AecToken.Int16(Version), AecToken.String(p.Description), AecToken.Int16(Version), AecToken.Int32(id),
                AecToken.String(p.Name), AecToken.Int16((short)p.Type), AecToken.Bool(false), AecToken.Hard(formatSlot),
            });
            t.AddRange(Value(AecValue.Default(p.Type)));
            t.AddRange(new[]
            {
                AecToken.Bool(false), AecToken.Int16(Version), AecToken.String(""), AecToken.Int16(Version),
                AecToken.Int32(0), AecToken.Int16(0), AecToken.String(""), AecToken.Byte(2), AecToken.Int32(0),
            });
        }
        t.Add(AecToken.Int16((short)appliesTo.Count));
        t.AddRange(appliesTo.Select(AecToken.String));
        t.AddRange(new[] { AecToken.Int16(3), AecToken.Bool(false), AecToken.Bool(false), AecToken.Byte(0) });
        t.AddRange(End());
        return t;
    }

    /// <param name="values">One value per property of the definition, in id order.</param>
    public static IReadOnlyList<AecToken> Set(int entitySlot, int definitionSlot, IReadOnlyList<AecValue> values)
    {
        var t = Head(TagSet, "");
        t.AddRange(new[]
        {
            AecToken.Int16(Version), AecToken.Hard(entitySlot), AecToken.Hard(definitionSlot),
            AecToken.Int16(Version), AecToken.String(""), AecToken.Int16(Version), AecToken.Int32(values.Count),
        });
        for (int id = 0; id < values.Count; id++)
            t.AddRange(Item(id, values[id]));
        t.AddRange(End());
        return t;
    }

    /// <summary>One 1006 record as Civil files it.</summary>
    public static IReadOnlyList<AecToken> Item(int id, AecValue value)
    {
        var t = new List<AecToken>
        {
            AecToken.Bool(true), AecToken.Bool(true), AecToken.Int16(TagItem),
            AecToken.Int16(Version), AecToken.String(""), AecToken.Int16(Version), AecToken.Int32(id),
            AecToken.Int16((short)value.Type),
        };
        t.AddRange(Value(value));
        t.AddRange(new[]
        {
            AecToken.Bool(false), AecToken.Int16(Version), AecToken.String(""), AecToken.Int16(Version),
            AecToken.Int32(0), AecToken.Int16(0),
        });
        return t;
    }

    private static List<AecToken> Head(short tag, string description) => new()
    {
        AecToken.Bool(true), AecToken.Bool(false), AecToken.Int16(Version),
        AecToken.Bool(true), AecToken.Bool(true), AecToken.Int16(tag),
        AecToken.Int16(Version), AecToken.String(description), AecToken.Int16(Version), AecToken.String(""),
        AecToken.Bool(false), AecToken.Byte(0),
    };

    private static IReadOnlyList<AecToken> End() => new[]
    {
        AecToken.Bool(false), AecToken.Int16(Version), AecToken.Int16(Version), AecToken.Int16(0),
    };

    private static IReadOnlyList<AecToken> Value(AecValue value) => value.Match(
        integer: i => new[] { AecToken.Int16(3), AecToken.Int32(i) },
        real: d => new[] { AecToken.Int16(5), AecToken.Double(d) },
        text: s => new[] { AecToken.Int16(8), AecToken.String(s) },
        trueFalse: b => new[] { AecToken.Int16(11), AecToken.Bool(b) });

    // ---------------------------------------------------------------- decoding

    public static Result<AecDefinition> ReadDefinition(IReadOnlyList<AecToken> tokens) =>
        Records(tokens, TagDefinition, TagProperty, DefinitionCountOffset, "definition").Bind(records =>
        {
            string description = Is(tokens, records.Header + 2, AecTokenKind.String) ? tokens[records.Header + 2].Text : "";
            var properties = new List<AecDefinedProperty>();
            foreach (int at in records.Items)
            {
                Result<AecDefinedProperty> property = PropertyAt(tokens, at);
                string fault = property.Match(p => { properties.Add(p); return ""; }, message => message);
                if (fault.Length > 0) return Result<AecDefinition>.Failure(fault);
            }
            var duplicate = properties.GroupBy(p => p.Id).FirstOrDefault(g => g.Count() > 1);
            if (duplicate is not null)
                return Result<AecDefinition>.Failure($"definition: property id {duplicate.Key} is defined twice");
            return Result<AecDefinition>.Success(new AecDefinition(description, properties));
        });

    public static Result<AecSet> ReadSet(IReadOnlyList<AecToken> tokens) =>
        Records(tokens, TagSet, TagItem, SetCountOffset, "property set").Bind(records =>
        {
            int headerEnd = records.Items.Count > 0 ? records.Items[0] - 2 : tokens.Count;
            var pointers = new List<int>();
            for (int i = records.Header; i < headerEnd; i++)
                if (tokens[i].Kind == AecTokenKind.HardPointer) pointers.Add((int)tokens[i].Integer);

            var items = new List<AecItem>();
            foreach (int at in records.Items)
            {
                if (!Kinds(tokens, at, (2, AecTokenKind.String), (4, AecTokenKind.Int32),
                        (5, AecTokenKind.Int16), (6, AecTokenKind.Int16)))
                    return Result<AecSet>.Failure($"property set: the value record at token {at} is not laid out as AEC lays it");
                int id = (int)tokens[at + 4].Integer;
                items.Add(DataTypeOf(tokens[at + 5].Integer).Match(
                    type => ValueAt(tokens, at + 6, type).Match(
                        value => value.Match<AecItem>(
                            v => new AecItem.Stored(id, at, v),
                            () => new AecItem.Empty(id, at, type)),
                        reason => new AecItem.Unreadable(id, at, reason)),
                    reason => new AecItem.Unreadable(id, at, reason)));
            }
            return Result<AecSet>.Success(new AecSet(pointers, items, records.Header + SetCountOffset));
        });

    private sealed record Found(int Header, IReadOnlyList<int> Items);

    // The header record and every property record, after checking each tag, each filing
    // marker and the header's record count. A record opens Bool, Bool, I16 tag, I16 80.
    private static Result<Found> Records(
        IReadOnlyList<AecToken> tokens, short headerTag, short itemTag, int countOffset, string what)
    {
        if (!EndsAsAecEnds(tokens))
            return Result<Found>.Failure($"{what}: the stream does not end as an AEC object ends (truncated, or tokens after the object)");
        var records = new List<int>();
        for (int i = 2; i + 1 < tokens.Count; i++)
            if (tokens[i - 2].Kind == AecTokenKind.Bool && tokens[i - 1].Kind == AecTokenKind.Bool
                && tokens[i].Kind == AecTokenKind.Int16 && tokens[i + 1].Kind == AecTokenKind.Int16)
                records.Add(i);
        if (records.Count == 0) return Result<Found>.Failure($"{what}: the stream holds no AEC record");

        int header = records[0];
        if (tokens[header].Integer != headerTag)
            return Result<Found>.Failure($"{what}: the object opens with record tag {tokens[header].Integer}, expected {headerTag}");
        if (header < 3 || !Marker(tokens, header - 3) || !Marker(tokens, header + 1))
            return Result<Found>.Failure($"{what}: the header's filing marker is not {Version}");

        var items = new List<int>();
        foreach (int at in records.Skip(1))
        {
            if (tokens[at].Integer != itemTag)
                return Result<Found>.Failure($"{what}: record tag {tokens[at].Integer} at token {at}, expected {itemTag}");
            if (!Marker(tokens, at + 1) || !Marker(tokens, at + 3))
                return Result<Found>.Failure($"{what}: the record at token {at} has a filing marker other than {Version}");
            items.Add(at);
        }

        int countAt = header + countOffset;
        if (!Is(tokens, countAt, AecTokenKind.Int32))
            return Result<Found>.Failure($"{what}: no record count at token {countAt}");
        if (tokens[countAt].Integer != items.Count)
            return Result<Found>.Failure($"{what}: the header counts {tokens[countAt].Integer} records, the stream holds {items.Count}");
        return Result<Found>.Success(new Found(header, items));
    }

    // One 1007 record. Its layout must hold; a property that is not a manual one of the
    // four data types is kept, with the reason it is not supported.
    private static Result<AecDefinedProperty> PropertyAt(IReadOnlyList<AecToken> tokens, int at)
    {
        if (!Kinds(tokens, at, (2, AecTokenKind.String), (4, AecTokenKind.Int32), (5, AecTokenKind.String),
                (6, AecTokenKind.Int16), (7, AecTokenKind.Bool), (9, AecTokenKind.Int16)))
            return Result<AecDefinedProperty>.Failure($"definition: the property record at token {at} is not laid out as AEC lays it");
        int id = (int)tokens[at + 4].Integer;
        string name = tokens[at + 5].Text;           // tokens[at + 2] is the DESCRIPTION
        string description = tokens[at + 2].Text;
        Result<AecDataType> type = tokens[at + 7].Integer != 0
            ? Result<AecDataType>.Failure("an automatic property: its value is computed, not stored")
            : DataTypeOf(tokens[at + 6].Integer).Bind(t =>
                // The default is not used, but its VARIANT must be one of the property's type.
                ValueAt(tokens, at + 9, t).Map(_ => t));
        return Result<AecDefinedProperty>.Success(new AecDefinedProperty(id, name, description, type));
    }

    private static Result<AecDataType> DataTypeOf(long filed) => filed is >= 0 and <= 3
        ? Result<AecDataType>.Success((AecDataType)filed)
        : Result<AecDataType>.Failure($"data type {filed} is not Integer, Real, Text or TrueFalse");

    // The VARIANT type at `at` and the value after it, which must belong to `type`. An empty
    // VARIANT fits every type and files no value.
    private static Result<Option<AecValue>> ValueAt(IReadOnlyList<AecToken> tokens, int at, AecDataType type)
    {
        long vt = tokens[at].Integer;
        if (vt is 0 or 1) return Result<Option<AecValue>>.Success(Option<AecValue>.Nothing);
        (AecDataType Family, AecTokenKind Kind)? known = vt switch
        {
            2 => (AecDataType.Integer, AecTokenKind.Int16),
            3 => (AecDataType.Integer, AecTokenKind.Int32),
            5 => (AecDataType.Real, AecTokenKind.Double),
            8 => (AecDataType.Text, AecTokenKind.String),
            11 => (AecDataType.TrueFalse, AecTokenKind.Bool),
            _ => null,
        };
        if (known is not { } k)
            return Result<Option<AecValue>>.Failure($"VARIANT type {vt} is not one this reader knows");
        if (k.Family != type)
            return Result<Option<AecValue>>.Failure($"a {k.Family} value (VARIANT {vt}) in a {type} property");
        if (!Is(tokens, at + 1, k.Kind))
            return Result<Option<AecValue>>.Failure($"VARIANT type {vt} has no value of its kind at token {at + 1}");
        AecToken v = tokens[at + 1];
        AecValue value = type switch
        {
            AecDataType.Integer => new AecValue.Integer((int)v.Integer),
            AecDataType.Real => new AecValue.Real(v.Real),
            AecDataType.Text => new AecValue.Text(v.Text),
            AecDataType.TrueFalse => new AecValue.TrueFalse(v.Integer != 0),
        };
        return Result<Option<AecValue>>.Success(Option<AecValue>.Of(value));
    }

    private static bool EndsAsAecEnds(IReadOnlyList<AecToken> tokens)
    {
        IReadOnlyList<AecToken> end = End();
        int tail = tokens.Count - end.Count;
        return tail >= 0 && end.Select((e, i) => tokens[tail + i] == e).All(x => x);
    }

    private static bool Is(IReadOnlyList<AecToken> tokens, int at, AecTokenKind kind) =>
        at >= 0 && at < tokens.Count && tokens[at].Kind == kind;

    private static bool Marker(IReadOnlyList<AecToken> tokens, int at) =>
        Is(tokens, at, AecTokenKind.Int16) && tokens[at].Integer == Version;

    private static bool Kinds(IReadOnlyList<AecToken> tokens, int at, params (int Offset, AecTokenKind Kind)[] fields) =>
        fields.All(f => Is(tokens, at + f.Offset, f.Kind));

    // ---------------------------------------------------------------- editing

    /// <summary>
    /// The set's stream with <paramref name="value"/> stored for property <paramref name="id"/>:
    /// the record's data type, VARIANT type and value replaced, or, when the set stores no
    /// record for the property, a new record before End and the record count raised by one.
    /// Nothing else in the stream changes.
    /// </summary>
    public static Result<IReadOnlyList<AecToken>> WithValue(
        IReadOnlyList<AecToken> tokens, AecSet set, int id, AecValue value)
    {
        IReadOnlyList<AecToken> filed = Value(value);
        return set.ById(id).Match(
            item => item.Match(
                stored => Replace(tokens, stored.At, filed, value.Type),
                empty => Replace(tokens, empty.At, filed, value.Type),
                unreadable => Result<IReadOnlyList<AecToken>>.Failure(
                    $"property {id} cannot be edited: {unreadable.Reason}")),
            () => Insert(tokens, set, id, value));
    }

    private static Result<IReadOnlyList<AecToken>> Replace(
        IReadOnlyList<AecToken> tokens, int at, IReadOnlyList<AecToken> filed, AecDataType type)
    {
        int from = at + 5;                                       // the data type
        int to = at + 7 + (tokens[at + 6].Integer is 0 or 1 ? 0 : 1); // past the value
        var t = new List<AecToken>(tokens.Count + 1);
        t.AddRange(tokens.Take(from));
        t.Add(AecToken.Int16((short)type));
        t.AddRange(filed);
        t.AddRange(tokens.Skip(to));
        return Result<IReadOnlyList<AecToken>>.Success(t);
    }

    private static Result<IReadOnlyList<AecToken>> Insert(IReadOnlyList<AecToken> tokens, AecSet set, int id, AecValue value)
    {
        IReadOnlyList<AecToken> end = End();
        int tail = tokens.Count - end.Count;
        if (!EndsAsAecEnds(tokens))
            return Result<IReadOnlyList<AecToken>>.Failure("the set does not end as AEC ends one; a record cannot be added");
        if (!Is(tokens, set.CountAt, AecTokenKind.Int32))
            return Result<IReadOnlyList<AecToken>>.Failure("the set's record count is not where it should be");
        var t = new List<AecToken>(tokens.Count + 16);
        t.AddRange(tokens.Take(tail));
        t[set.CountAt] = AecToken.Int32((int)tokens[set.CountAt].Integer + 1);
        t.AddRange(Item(id, value));
        t.AddRange(end);
        return Result<IReadOnlyList<AecToken>>.Success(t);
    }
}
