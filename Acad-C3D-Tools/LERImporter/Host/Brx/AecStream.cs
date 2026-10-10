using System;
using System.Collections.Generic;
using System.Linq;

using LERImporter.PropertySets;

// LerDataType switches name every member and have no discard arm (see LerPropertySetSchema.cs).
#pragma warning disable CS8524

namespace LERImporter.Host.Brx;

// The DWG filer stream of AEC's property-set objects, as BricsCAD's AEC classes (ODA's
// AecScheduleData) file and read it. BricsCAD has no managed AEC API, so LERImporter makes
// the objects itself: it creates an empty one by class and hands it this stream through
// DwgIn. Pure: no host types, so the codec is tested without CAD.
//
// MEASURED on BricsCAD V26.2 (2026-10-10), recording a file filer over sets and definitions
// that Civil 3D 2025 wrote (Svogerslev LER, 1,981 sets, 40,075 values): every object has the
// layout below, with no variation outside the values. A set and a definition this codec
// wrote, saved by BricsCAD, read back in Civil 3D's AEC API value for value.
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
//                   Str name, I16 data type, Bool false, HPtr format, Value(default),
//                   Bool false, I16 80, Str "", I16 80, I32 0, I16 0, Str "", Byte 2, I32 0
//   1001 set        I16 80, HPtr entity, HPtr definition, I16 80, Str "", I16 80, I32 n,
//                   n x 1006 item, then End
//   1006 item       Bool true, Bool true, I16 1006, I16 80, Str "", I16 80, I32 id,
//                   I16 data type, Value, Bool false, I16 80, Str "", I16 80, I32 0, I16 0
//   Value           I16 variant type, then the value: 3 I32 (Integer), 5 Double (Real),
//                   8 Str (Text), 11 Bool (TrueFalse)
//   End             Bool false, I16 80, I16 80, I16 0
// 80 is AEC's filing version. Fields not named here (the Bool falses, Byte 2, the trailing
// I32 0 / I16 0) are filed as Civil files them for a manual property.

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

/// <summary>A decoded definition: its properties by id, and the slot of its format.</summary>
internal sealed record AecDefinition(string Description, IReadOnlyList<LerPropertyDef> Properties, int FormatSlot);

/// <summary>A decoded set: the slots of its entity and definition, and its values by id.</summary>
internal sealed record AecSet(int EntitySlot, int DefinitionSlot, IReadOnlyList<(int Id, LerValue Value)> Values);

internal static class AecStream
{
    private const short Version = 80;
    private const short TagSet = 1001;
    private const short TagDefinition = 1002;
    private const short TagFormat = 1003;
    private const short TagItem = 1006;
    private const short TagProperty = 1007;

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

    /// <param name="formatSlot">The slot of the format every property points at.</param>
    public static IReadOnlyList<AecToken> Definition(LerSetDef def, int formatSlot)
    {
        int n = def.Properties.Count;
        var t = Head(TagDefinition, def.Description);
        t.AddRange(new[]
        {
            AecToken.Int16(Version), AecToken.Int32(n),
            AecToken.Int16(Version), AecToken.String(""), AecToken.Int16(Version), AecToken.Int32(n),
        });
        for (int id = 0; id < n; id++)
        {
            LerPropertyDef p = def.Properties[id];
            t.AddRange(new[]
            {
                AecToken.Bool(true), AecToken.Bool(true), AecToken.Int16(TagProperty),
                AecToken.Int16(Version), AecToken.String(p.Description), AecToken.Int16(Version), AecToken.Int32(id),
                AecToken.String(p.Name), AecToken.Int16((short)p.Type), AecToken.Bool(false), AecToken.Hard(formatSlot),
            });
            t.AddRange(Value(LerValue.Default(p.Type)));
            t.AddRange(new[]
            {
                AecToken.Bool(false), AecToken.Int16(Version), AecToken.String(""), AecToken.Int16(Version),
                AecToken.Int32(0), AecToken.Int16(0), AecToken.String(""), AecToken.Byte(2), AecToken.Int32(0),
            });
        }
        t.Add(AecToken.Int16((short)LerSetDef.AppliesTo.Count));
        t.AddRange(LerSetDef.AppliesTo.Select(AecToken.String));
        t.AddRange(new[] { AecToken.Int16(3), AecToken.Bool(false), AecToken.Bool(false), AecToken.Byte(0) });
        t.AddRange(End());
        return t;
    }

    /// <param name="values">One value per property of the definition, in id order.</param>
    public static IReadOnlyList<AecToken> Set(int entitySlot, int definitionSlot, IReadOnlyList<(LerDataType Type, LerValue Value)> values)
    {
        var t = Head(TagSet, "");
        t.AddRange(new[]
        {
            AecToken.Int16(Version), AecToken.Hard(entitySlot), AecToken.Hard(definitionSlot),
            AecToken.Int16(Version), AecToken.String(""), AecToken.Int16(Version), AecToken.Int32(values.Count),
        });
        for (int id = 0; id < values.Count; id++)
        {
            t.AddRange(new[]
            {
                AecToken.Bool(true), AecToken.Bool(true), AecToken.Int16(TagItem),
                AecToken.Int16(Version), AecToken.String(""), AecToken.Int16(Version), AecToken.Int32(id),
                AecToken.Int16((short)values[id].Type),
            });
            t.AddRange(Value(values[id].Value));
            t.AddRange(new[]
            {
                AecToken.Bool(false), AecToken.Int16(Version), AecToken.String(""), AecToken.Int16(Version),
                AecToken.Int32(0), AecToken.Int16(0),
            });
        }
        t.AddRange(End());
        return t;
    }

    private static List<AecToken> Head(short tag, string description) => new()
    {
        AecToken.Bool(true), AecToken.Bool(false), AecToken.Int16(Version),
        AecToken.Bool(true), AecToken.Bool(true), AecToken.Int16(tag),
        AecToken.Int16(Version), AecToken.String(description), AecToken.Int16(Version), AecToken.String(""),
        AecToken.Bool(false), AecToken.Byte(0),
    };

    private static IEnumerable<AecToken> End() => new[]
    {
        AecToken.Bool(false), AecToken.Int16(Version), AecToken.Int16(Version), AecToken.Int16(0),
    };

    private static IEnumerable<AecToken> Value(LerValue value) => value.Match(
        integer: i => new[] { AecToken.Int16(3), AecToken.Int32(i) },
        real: d => new[] { AecToken.Int16(5), AecToken.Double(d) },
        text: s => new[] { AecToken.Int16(8), AecToken.String(s) },
        trueFalse: b => new[] { AecToken.Int16(11), AecToken.Bool(b) });

    // ---------------------------------------------------------------- decoding

    public static Result<AecDefinition> ReadDefinition(IReadOnlyList<AecToken> tokens)
    {
        var r = new Reader(tokens);
        string description = "";
        if (!r.Head(TagDefinition, d => description = d)) return r.Fail<AecDefinition>();
        if (!r.I16(Version)) return r.Fail<AecDefinition>();
        int n = r.AnyI32();
        if (!r.I16(Version) || !r.Str("") || !r.I16(Version)) return r.Fail<AecDefinition>();
        r.AnyI32();
        var properties = new List<LerPropertyDef>();
        int formatSlot = -1;
        for (int i = 0; i < n; i++)
        {
            if (!r.Bool(true) || !r.Bool(true) || !r.I16(TagProperty) || !r.I16(Version)) return r.Fail<AecDefinition>();
            string desc = r.AnyStr();
            if (!r.I16(Version)) return r.Fail<AecDefinition>();
            int id = r.AnyI32();
            string name = r.AnyStr();
            Option<LerDataType> type = r.DataType();
            if (!r.Bool(false)) return r.Fail<AecDefinition>();
            formatSlot = r.Pointer(AecTokenKind.HardPointer);
            if (id != i) return r.Fail<AecDefinition>($"property {i} has id {id}: ids out of order");
            Option<LerValue> defaultValue = type.Match(r.ValueOf, () => Option<LerValue>.Nothing);
            if (defaultValue.Match(_ => false, () => true)) return r.Fail<AecDefinition>();
            if (!r.Bool(false) || !r.I16(Version) || !r.Str("") || !r.I16(Version) || !r.I32(0) || !r.I16(0)
                || !r.Str("") || !r.Byte(2) || !r.I32(0)) return r.Fail<AecDefinition>();
            properties.Add(new LerPropertyDef(name, desc, type.OrElse(LerDataType.Text)));
        }
        int k = r.AnyI16();
        for (int i = 0; i < k; i++) r.AnyStr();
        if (!r.I16(3) || !r.Bool(false) || !r.Bool(false) || !r.Byte(0) || !r.End()) return r.Fail<AecDefinition>();
        return r.Ok(new AecDefinition(description, properties, formatSlot));
    }

    public static Result<AecSet> ReadSet(IReadOnlyList<AecToken> tokens)
    {
        var r = new Reader(tokens);
        if (!r.Head(TagSet, _ => { }) || !r.I16(Version)) return r.Fail<AecSet>();
        int entity = r.Pointer(AecTokenKind.HardPointer);
        int definition = r.Pointer(AecTokenKind.HardPointer);
        if (!r.I16(Version) || !r.Str("") || !r.I16(Version)) return r.Fail<AecSet>();
        int n = r.AnyI32();
        var values = new List<(int, LerValue)>();
        for (int i = 0; i < n; i++)
        {
            if (!r.Bool(true) || !r.Bool(true) || !r.I16(TagItem) || !r.I16(Version) || !r.Str("") || !r.I16(Version))
                return r.Fail<AecSet>();
            int id = r.AnyI32();
            Option<LerValue> value = r.DataType().Match(r.ValueOf, () => Option<LerValue>.Nothing);
            if (!r.Bool(false) || !r.I16(Version) || !r.Str("") || !r.I16(Version) || !r.I32(0) || !r.I16(0))
                return r.Fail<AecSet>();
            if (value.Match(v => { values.Add((id, v)); return false; }, () => true)) return r.Fail<AecSet>();
        }
        if (!r.End()) return r.Fail<AecSet>();
        return r.Ok(new AecSet(entity, definition, values));
    }

    /// <summary>
    /// A strict reader: the first token that is not what the layout says stops it, and
    /// the decode is refused with that token named.
    /// </summary>
    private sealed class Reader
    {
        private readonly IReadOnlyList<AecToken> _t;
        private int _i;
        private string _error = "";

        public Reader(IReadOnlyList<AecToken> tokens) { _t = tokens; }

        public Result<T> Fail<T>() => Result<T>.Failure(_error.Length > 0 ? _error : $"unexpected stream at token {_i}");
        public Result<T> Fail<T>(string message) => Result<T>.Failure(message);
        public Result<T> Ok<T>(T value) =>
            _error.Length > 0 ? Fail<T>()
            : _i != _t.Count ? Result<T>.Failure($"{_t.Count - _i} tokens left after the object")
            : Result<T>.Success(value);

        private Option<AecToken> Next(AecTokenKind kind)
        {
            if (_error.Length > 0) return Option<AecToken>.Nothing;
            if (_i >= _t.Count) { _error = $"stream ends at token {_i}, wanted {kind}"; return Option<AecToken>.Nothing; }
            AecToken token = _t[_i++];
            if (token.Kind != kind)
            {
                _error = $"token {_i - 1} is {token}, wanted {kind}";
                return Option<AecToken>.Nothing;
            }
            return Option<AecToken>.Of(token);
        }

        private bool Is(AecTokenKind kind, Func<AecToken, bool> test, string wanted) => Next(kind).Match(
            token =>
            {
                if (test(token)) return true;
                _error = $"token {_i - 1} is {token}, wanted {wanted}";
                return false;
            },
            () => false);

        public bool Bool(bool v) => Is(AecTokenKind.Bool, t => (t.Integer != 0) == v, "Bool " + v);
        public bool Byte(byte v) => Is(AecTokenKind.Byte, t => t.Integer == v, "Byte " + v);
        public bool I16(short v) => Is(AecTokenKind.Int16, t => t.Integer == v, "I16 " + v);
        public bool I32(int v) => Is(AecTokenKind.Int32, t => t.Integer == v, "I32 " + v);
        public bool Str(string v) => Is(AecTokenKind.String, t => t.Text == v, "Str " + v);
        public int AnyI16() => Next(AecTokenKind.Int16).Match(t => (int)t.Integer, () => 0);
        public int AnyI32() => Next(AecTokenKind.Int32).Match(t => (int)t.Integer, () => 0);
        public string AnyStr() => Next(AecTokenKind.String).Match(t => t.Text, () => "");
        public int Pointer(AecTokenKind kind) => Next(kind).Match(t => (int)t.Integer, () => -1);

        public bool Head(short tag, Action<string> description)
        {
            if (!Bool(true) || !Bool(false) || !I16(Version) || !Bool(true) || !Bool(true) || !I16(tag) || !I16(Version))
                return false;
            description(AnyStr());
            return I16(Version) && Str("") && Bool(false) && Byte(0);
        }

        public bool End() => Bool(false) && I16(Version) && I16(Version) && I16(0);

        public Option<LerDataType> DataType()
        {
            int raw = AnyI16();
            if (_error.Length > 0) return Option<LerDataType>.Nothing;
            if (raw is >= 0 and <= 3) return Option<LerDataType>.Of((LerDataType)raw);
            _error = $"token {_i - 1}: data type {raw} is not Integer, Real, Text or TrueFalse";
            return Option<LerDataType>.Nothing;
        }

        /// <summary>A value of the given data type: the variant type must be the one AEC files for it.</summary>
        public Option<LerValue> ValueOf(LerDataType type)
        {
            short variant = type switch
            {
                LerDataType.Integer => 3,
                LerDataType.Real => 5,
                LerDataType.Text => 8,
                LerDataType.TrueFalse => 11,
            };
            if (!I16(variant)) return Option<LerValue>.Nothing;
            return type switch
            {
                LerDataType.Integer => Next(AecTokenKind.Int32).Match(
                    t => Option<LerValue>.Of(new LerValue.Integer((int)t.Integer)), () => Option<LerValue>.Nothing),
                LerDataType.Real => Next(AecTokenKind.Double).Match(
                    t => Option<LerValue>.Of(new LerValue.Real(t.Real)), () => Option<LerValue>.Nothing),
                LerDataType.Text => Next(AecTokenKind.String).Match(
                    t => Option<LerValue>.Of(new LerValue.Text(t.Text)), () => Option<LerValue>.Nothing),
                LerDataType.TrueFalse => Next(AecTokenKind.Bool).Match(
                    t => Option<LerValue>.Of(new LerValue.TrueFalse(t.Integer != 0)), () => Option<LerValue>.Nothing),
            };
        }
    }
}
