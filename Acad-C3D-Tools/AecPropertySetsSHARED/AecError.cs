using System;
using System.Collections.Generic;
using System.Linq;

using CSharpFunctionalExtensions;

namespace Norsyn.AecPropertySets;

/// <summary>
/// Why a property-set operation failed: the codec's one error contract, with a code a
/// caller can act on and a message a drafter can read. "There may be none" is a Maybe,
/// "this can fail" a Result&lt;T, AecError&gt; or UnitResult&lt;AecError&gt; (CSharpFunctionalExtensions).
/// </summary>
internal sealed record AecError(string Code, string Message)
{
    /// <summary>The filed stream is not laid out as AEC lays it out, or an object refused it.</summary>
    public const string StreamCode = "AEC_STREAM";

    /// <summary>A value is not one the property's data type takes.</summary>
    public const string ValueCode = "AEC_VALUE";

    /// <summary>
    /// A property or value this code does not read or write: not a manual Integer, Real, Text
    /// or True-False one. It is shown, never edited.
    /// </summary>
    public const string UnsupportedCode = "AEC_UNSUPPORTED";

    /// <summary>The drawing does not hold what the operation needs (a set, a definition, a property, a class).</summary>
    public const string DrawingCode = "AEC_DRAWING";

    /// <summary>The CAD program threw.</summary>
    public const string HostCode = "AEC_HOST";

    public static AecError Stream(string message) => new(StreamCode, message);

    public static AecError Value(string message) => new(ValueCode, message);

    public static AecError Unsupported(string message) => new(UnsupportedCode, message);

    public static AecError Drawing(string message) => new(DrawingCode, message);

    public static AecError Host(string what, Exception ex) =>
        new(HostCode, $"Property sets, {what}: {ex.GetType().Name}: {ex.Message}");

    /// <summary>The same error, its message prefixed with where it happened.</summary>
    public AecError In(string context) => this with { Message = $"{context}: {Message}" };

    public override string ToString() => Message;
}

internal static class AecResults
{
    /// <summary>
    /// Each item's result, in order, until the first failure, which is the result.
    /// </summary>
    public static Result<IReadOnlyList<TOut>, E> Traverse<TIn, TOut, E>(
        this IEnumerable<TIn> items, Func<TIn, Result<TOut, E>> each) =>
        items.Aggregate(
            Result.Success<List<TOut>, E>(new List<TOut>()),
            (done, item) => done.Bind(list => each(item).Map(value =>
            {
                list.Add(value);
                return list;
            })))
        .Map(list => (IReadOnlyList<TOut>)list);

    /// <summary>Each item's check, in order, until the first failure, which is the result.</summary>
    public static UnitResult<E> Each<TIn, E>(this IEnumerable<TIn> items, Func<TIn, UnitResult<E>> each) =>
        items.Aggregate(UnitResult.Success<E>(), (done, item) => done.Bind(() => each(item)));
}
