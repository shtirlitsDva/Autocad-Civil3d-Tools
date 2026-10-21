using System;
using System.Collections.Generic;
using System.Globalization;

// AecDataType switches name every member and have no discard arm, so a new member
// fails to compile where it is not handled; CS8524 only asks for unnamed values.
#pragma warning disable CS8524

namespace Norsyn.AecPropertySets;

/// <summary>
/// The four data types a manual AEC property can have, numbered as AEC's DataType is (and
/// as the drawing files them). Automatic, formula, classification and material properties
/// are not supported.
/// </summary>
internal enum AecDataType : short
{
    Integer = 0,
    Real = 1,
    Text = 2,
    TrueFalse = 3,
}

/// <summary>A property of a definition this code writes.</summary>
internal sealed record AecPropertyDef(string Name, string Description, AecDataType Type);

/// <summary>One stored property value. The case is the property's data type.</summary>
internal abstract record AecValue
{
    private AecValue() { }

    public abstract TOut Match<TOut>(
        Func<int, TOut> integer, Func<double, TOut> real, Func<string, TOut> text, Func<bool, TOut> trueFalse);

    public AecDataType Type => Match(
        _ => AecDataType.Integer, _ => AecDataType.Real, _ => AecDataType.Text, _ => AecDataType.TrueFalse);

    /// <summary>The value as text, culture-invariant: what a drafter reads and a filter matches.</summary>
    public string ToText() => Match(
        i => i.ToString(CultureInfo.InvariantCulture),
        d => d.ToString(CultureInfo.InvariantCulture),
        s => s,
        b => b ? "True" : "False");

    public static AecValue Default(AecDataType type) => type switch
    {
        AecDataType.Integer => new Integer(0),
        AecDataType.Real => new Real(0.0),
        AecDataType.Text => new Text(""),
        AecDataType.TrueFalse => new TrueFalse(false),
    };

    /// <summary>
    /// A CLR value for a property of the given type, converted as AEC's PropertySet.SetAt
    /// converts it. A value AEC refuses is a fault.
    /// </summary>
    public static Result<AecValue> From(AecDataType type, object? value, string propertyName)
    {
        if (value is null) return Result<AecValue>.Success(Default(type));
        return type switch
        {
            AecDataType.Text => Result<AecValue>.Success(
                new Text(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "")),
            AecDataType.TrueFalse => value is bool b
                ? Result<AecValue>.Success(new TrueFalse(b))
                : Refused(type, value, propertyName),
            AecDataType.Real => value switch
            {
                double d => Result<AecValue>.Success(new Real(d)),
                float f => Result<AecValue>.Success(new Real(f)),
                int i => Result<AecValue>.Success(new Real(i)),
                long l => Result<AecValue>.Success(new Real(l)),
                _ => Refused(type, value, propertyName),
            },
            AecDataType.Integer => value switch
            {
                int i => Result<AecValue>.Success(new Integer(i)),
                short s => Result<AecValue>.Success(new Integer(s)),
                _ => Refused(type, value, propertyName),
            },
        };
    }

    /// <summary>
    /// What a drafter typed, as a value of the given type. A decimal comma and a decimal
    /// point both read as the decimal separator; True/False as AEC spells them.
    /// </summary>
    public static Result<AecValue> Parse(AecDataType type, string text, string propertyName)
    {
        string trimmed = text.Trim();
        return type switch
        {
            AecDataType.Text => Result<AecValue>.Success(new Text(text)),
            AecDataType.Integer => int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)
                ? Result<AecValue>.Success(new Integer(i))
                : Result<AecValue>.Failure($"{propertyName}: '{text}' is not a whole number."),
            AecDataType.Real => double.TryParse(trimmed.Replace(',', '.'), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double d) && double.IsFinite(d)
                ? Result<AecValue>.Success(new Real(d))
                : Result<AecValue>.Failure($"{propertyName}: '{text}' is not a number."),
            AecDataType.TrueFalse => trimmed.ToUpperInvariant() switch
            {
                "TRUE" => Result<AecValue>.Success(new TrueFalse(true)),
                "FALSE" => Result<AecValue>.Success(new TrueFalse(false)),
                _ => Result<AecValue>.Failure($"{propertyName}: '{text}' is not True or False."),
            },
        };
    }

    private static Result<AecValue> Refused(AecDataType type, object value, string propertyName) =>
        Result<AecValue>.Failure(
            $"Property {propertyName}: a {value.GetType().Name} '{value}' is not a {type} value.");

    internal sealed record Integer(int Value) : AecValue
    {
        public override TOut Match<TOut>(
            Func<int, TOut> integer, Func<double, TOut> real, Func<string, TOut> text, Func<bool, TOut> trueFalse) =>
            integer(Value);
    }

    internal sealed record Real(double Value) : AecValue
    {
        public override TOut Match<TOut>(
            Func<int, TOut> integer, Func<double, TOut> real, Func<string, TOut> text, Func<bool, TOut> trueFalse) =>
            real(Value);
    }

    internal sealed record Text(string Value) : AecValue
    {
        public override TOut Match<TOut>(
            Func<int, TOut> integer, Func<double, TOut> real, Func<string, TOut> text, Func<bool, TOut> trueFalse) =>
            text(Value);
    }

    internal sealed record TrueFalse(bool Value) : AecValue
    {
        public override TOut Match<TOut>(
            Func<int, TOut> integer, Func<double, TOut> real, Func<string, TOut> text, Func<bool, TOut> trueFalse) =>
            trueFalse(Value);
    }
}
