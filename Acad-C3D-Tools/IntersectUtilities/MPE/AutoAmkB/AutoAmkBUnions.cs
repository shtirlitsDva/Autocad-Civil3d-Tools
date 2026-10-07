using System;
using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.MPE.AutoAmkB;

// AUTOAMKB's vocabulary for "there may be none" and "this can fail" (AGENTS.md, null-and-exceptions).
// IntersectUtilities is C# 12, so the closed unions of GDALService are spelled as abstract records with a
// private constructor, the same way PipePlan does it: the cases are nested, nothing outside can add one,
// and every case answers Match itself, so a new case breaks the build wherever it is not handled yet.

internal abstract record Option<T>
{
    private Option() { }

    public abstract TOut Match<TOut>(Func<T, TOut> some, Func<TOut> none);

    public static Option<T> Of(T value) => new Some(value);

    public static readonly Option<T> Nothing = new None();

    internal sealed record Some(T Value) : Option<T>
    {
        public override TOut Match<TOut>(Func<T, TOut> some, Func<TOut> none) => some(Value);
    }

    internal sealed record None : Option<T>
    {
        public override TOut Match<TOut>(Func<T, TOut> some, Func<TOut> none) => none();
    }
}

internal abstract record Result<T>
{
    private Result() { }

    public abstract TOut Match<TOut>(Func<T, TOut> ok, Func<string, TOut> fault);

    public static Result<T> Success(T value) => new Ok(value);

    public static Result<T> Failure(string message) => new Fault(message);

    internal sealed record Ok(T Value) : Result<T>
    {
        public override TOut Match<TOut>(Func<T, TOut> ok, Func<string, TOut> fault) => ok(Value);
    }

    /// <summary>An expected failure. <see cref="Message"/> is user-facing (Danish).</summary>
    internal sealed record Fault(string Message) : Result<T>
    {
        public override TOut Match<TOut>(Func<T, TOut> ok, Func<string, TOut> fault) => fault(Message);
    }
}

internal static class AutoAmkBUnionExtensions
{
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> next) =>
        result.Match(next, Result<TOut>.Failure);

    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map) =>
        result.Match(value => Result<TOut>.Success(map(value)), Result<TOut>.Failure);

    public static void Switch<T>(this Result<T> result, Action<T> ok, Action<string> fault) =>
        result.Match<Action>(value => () => ok(value), message => () => fault(message))();

    public static void Switch<T>(this Option<T> option, Action<T> some, Action none) =>
        option.Match<Action>(value => () => some(value), () => none)();

    public static T OrElse<T>(this Option<T> option, T fallback) =>
        option.Match(value => value, () => fallback);

    public static Option<TOut> Map<TIn, TOut>(this Option<TIn> option, Func<TIn, TOut> map) =>
        option.Match(value => Option<TOut>.Of(map(value)), () => Option<TOut>.Nothing);

    public static Option<TOut> Bind<TIn, TOut>(this Option<TIn> option, Func<TIn, Option<TOut>> next) =>
        option.Match(next, () => Option<TOut>.Nothing);

    public static Option<T> Where<T>(this Option<T> option, Func<T, bool> keep) =>
        option.Match(value => keep(value) ? Option<T>.Of(value) : Option<T>.Nothing, () => Option<T>.Nothing);

    public static bool IsSome<T>(this Option<T> option) => option.Match(_ => true, () => false);

    /// <summary>The values of the cases that have one, in order.</summary>
    public static IEnumerable<T> Values<T>(this IEnumerable<Option<T>> options) =>
        options.SelectMany(option => option.Match(value => new[] { value }, Array.Empty<T>));

    /// <summary>The first element, or none for an empty sequence.</summary>
    public static Option<T> FirstOption<T>(this IEnumerable<T> source)
    {
        foreach (T item in source) return Option<T>.Of(item);
        return Option<T>.Nothing;
    }

    public static Option<TValue> Lookup<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key) =>
        dictionary.TryGetValue(key, out var value) ? Option<TValue>.Of(value) : Option<TValue>.Nothing;

    /// <summary>The fault message of a failed result, as an option, for collecting warnings.</summary>
    public static Option<string> FaultMessage<T>(this Result<T> result) =>
        result.Match(_ => Option<string>.Nothing, Option<string>.Of);
}

/// <summary>
/// The only place AUTOAMKB catches exceptions: calls into AutoCAD, Civil 3D, the file system and other
/// third-party code that report failure by throwing. Each call is converted to a Result or an Option here,
/// so the rest of the tool never uses exceptions for control flow.
/// </summary>
internal static class Boundary
{
    public static Result<T> Try<T>(Func<T> call, string context)
    {
        try
        {
            return Result<T>.Success(call());
        }
        catch (System.Exception ex)
        {
            prdDbg(ex);
            return Result<T>.Failure($"{context}: {ex.Message}");
        }
    }

    /// <summary>For hot loops where a failure simply means "no value here" (sampling past a profile end).</summary>
    public static Option<T> TryOption<T>(Func<T> call)
    {
        try
        {
            return Option<T>.Of(call());
        }
        catch (System.Exception)
        {
            return Option<T>.Nothing;
        }
    }
}
