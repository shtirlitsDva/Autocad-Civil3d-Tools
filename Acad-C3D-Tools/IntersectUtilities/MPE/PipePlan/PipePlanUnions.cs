namespace IntersectUtilities.MPE.PipePlan;

// PipePlan's vocabulary for "there may be none" and "this can fail" (AGENTS.md,
// null-and-exceptions). IntersectUtilities is C# 12, so the closed unions of
// GDALService are spelled as abstract records with a private constructor: the
// cases are nested, nothing outside can add one, and every case answers Match
// itself, so a new case breaks the build wherever it is not handled yet.

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

internal static class PipePlanUnionExtensions
{
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> next) =>
        result.Match(next, Result<TOut>.Failure);

    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map) =>
        result.Match(value => Result<TOut>.Success(map(value)), Result<TOut>.Failure);

    // A side effect per case, so callers never test a case by type.
    public static void Switch<T>(this Result<T> result, Action<T> ok, Action<string> fault) =>
        result.Match<Action>(value => () => ok(value), message => () => fault(message))();

    public static void Switch<T>(this Option<T> option, Action<T> some, Action none) =>
        option.Match<Action>(value => () => some(value), () => none)();

    public static T OrElse<T>(this Option<T> option, T fallback) =>
        option.Match(value => value, () => fallback);
}
