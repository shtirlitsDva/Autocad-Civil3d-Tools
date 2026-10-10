using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

// LerDataType switches name every member and have no discard arm, so a new member
// fails to compile where it is not handled; CS8524 only asks for unnamed values.
#pragma warning disable CS8524

namespace LERImporter.PropertySets;

/// <summary>
/// The four AEC data types a LER property can have, numbered as AEC's DataType is
/// (and as the drawing files them). Only manual properties exist: no automatic,
/// formula or classification properties.
/// </summary>
internal enum LerDataType : short
{
    Integer = 0,
    Real = 1,
    Text = 2,
    TrueFalse = 3,
}

internal sealed record LerPropertyDef(string Name, string Description, LerDataType Type);

/// <summary>
/// A property set definition as LERImporter makes it: one per GML type, named after
/// the type ("AfloebsledningType" -> "Afloebsledning").
/// </summary>
internal sealed record LerSetDef(string Name, string Description, IReadOnlyList<LerPropertyDef> Properties)
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
        List<LerPropertyDef> properties = type.GetProperties()
            .Where(p => p.CustomAttributes.Any(a => a.AttributeType == typeof(Schema.PsInclude)))
            .Select(p => new LerPropertyDef(p.Name, p.Name, DataTypeOf(p.PropertyType)))
            .ToList();
        properties.Add(new LerPropertyDef(
            "GmlBemærkning", "The bemærkning for graverforespørgsel.", LerDataType.Text));
        properties.Add(new LerPropertyDef("LerNummer", "Ler nummer.", LerDataType.Text));
        return new LerSetDef(type.Name.Replace("Type", ""), type.FullName ?? type.Name, properties);
    }

    private static LerDataType DataTypeOf(Type type) => type.Name switch
    {
        nameof(String) => LerDataType.Text,
        nameof(Boolean) => LerDataType.TrueFalse,
        nameof(Double) => LerDataType.Real,
        nameof(Int32) => LerDataType.Integer,
        _ => LerDataType.Text,
    };
}

/// <summary>One stored property value. The case is the property's data type.</summary>
internal abstract record LerValue
{
    private LerValue() { }

    public abstract TOut Match<TOut>(
        Func<int, TOut> integer, Func<double, TOut> real, Func<string, TOut> text, Func<bool, TOut> trueFalse);

    public static LerValue Default(LerDataType type) => type switch
    {
        LerDataType.Integer => new Integer(0),
        LerDataType.Real => new Real(0.0),
        LerDataType.Text => new Text(""),
        LerDataType.TrueFalse => new TrueFalse(false),
    };

    /// <summary>
    /// A value from the GML object for a property of the given type, converted as AEC's
    /// PropertySet.SetAt converts it. A value AEC refuses is a fault.
    /// </summary>
    public static Result<LerValue> From(LerDataType type, object? value, string propertyName)
    {
        if (value is null) return Result<LerValue>.Success(Default(type));
        return type switch
        {
            LerDataType.Text => Result<LerValue>.Success(
                new Text(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "")),
            LerDataType.TrueFalse => value is bool b
                ? Result<LerValue>.Success(new TrueFalse(b))
                : Refused(type, value, propertyName),
            LerDataType.Real => value switch
            {
                double d => Result<LerValue>.Success(new Real(d)),
                float f => Result<LerValue>.Success(new Real(f)),
                int i => Result<LerValue>.Success(new Real(i)),
                long l => Result<LerValue>.Success(new Real(l)),
                _ => Refused(type, value, propertyName),
            },
            LerDataType.Integer => value switch
            {
                int i => Result<LerValue>.Success(new Integer(i)),
                short s => Result<LerValue>.Success(new Integer(s)),
                _ => Refused(type, value, propertyName),
            },
        };
    }

    private static Result<LerValue> Refused(LerDataType type, object value, string propertyName) =>
        Result<LerValue>.Failure(
            $"Property {propertyName}: a {value.GetType().Name} '{value}' is not a {type} value.");

    internal sealed record Integer(int Value) : LerValue
    {
        public override TOut Match<TOut>(
            Func<int, TOut> integer, Func<double, TOut> real, Func<string, TOut> text, Func<bool, TOut> trueFalse) =>
            integer(Value);
    }

    internal sealed record Real(double Value) : LerValue
    {
        public override TOut Match<TOut>(
            Func<int, TOut> integer, Func<double, TOut> real, Func<string, TOut> text, Func<bool, TOut> trueFalse) =>
            real(Value);
    }

    internal sealed record Text(string Value) : LerValue
    {
        public override TOut Match<TOut>(
            Func<int, TOut> integer, Func<double, TOut> real, Func<string, TOut> text, Func<bool, TOut> trueFalse) =>
            text(Value);
    }

    internal sealed record TrueFalse(bool Value) : LerValue
    {
        public override TOut Match<TOut>(
            Func<int, TOut> integer, Func<double, TOut> real, Func<string, TOut> text, Func<bool, TOut> trueFalse) =>
            trueFalse(Value);
    }
}
